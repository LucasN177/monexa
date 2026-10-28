using monexa.Models;
using monexa.Models.Enums;
using monexa.Services.Data;
using MudBlazor;

namespace monexa.Services;

/// <summary>
/// Zentraler Anwendungszustand: hält die Daten des aktuell gewählten Projekts,
/// kapselt alle Schreiboperationen und stellt Auswertungen bereit.
/// Seiten abonnieren <see cref="Changed"/>, um nach Änderungen neu zu rendern.
/// </summary>
public class FinanceState
{
    private readonly IDataStore _store;
    private readonly AuthService _auth;
    private readonly PreferencesService _preferences;
    private readonly ISnackbar _snackbar;
    private readonly Supabase.Client? _client;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public FinanceState(IDataStore store, AuthService auth, PreferencesService preferences, ISnackbar snackbar,
        Supabase.Client? client = null)
    {
        _store = store;
        _auth = auth;
        _preferences = preferences;
        _snackbar = snackbar;
        _client = client;
    }

    public event Action? Changed;

    public bool IsLoaded { get; private set; }
    public bool IsLoading { get; private set; }
    public string? LoadError { get; private set; }
    public string StoreName => _store.Name;

    public List<Project> Projects { get; private set; } = new();
    public Project? CurrentProject { get; private set; }
    public List<ProjectMember> Members { get; private set; } = new();
    public List<Konto> Accounts { get; private set; } = new();
    public List<Transaction> Transactions { get; private set; } = new();
    public List<Category> Categories { get; private set; } = new();
    public List<Budget> Budgets { get; private set; } = new();
    public List<RecurringTransaction> Recurring { get; private set; } = new();
    public List<SavingsGoal> Goals { get; private set; } = new();

    /// <summary>Rolle des angemeldeten Nutzers im aktuellen Projekt. Im Demo-Modus immer Admin.</summary>
    public ProjectRole CurrentRole
    {
        get
        {
            if (!_auth.IsCloud || CurrentProject == null) return ProjectRole.Admin;
            if (CurrentProject.OwnerId != null && CurrentProject.OwnerId == _auth.UserId) return ProjectRole.Admin;
            return Members.FirstOrDefault(m => m.UserId != null && m.UserId == _auth.UserId)?.Role ?? ProjectRole.Leser;
        }
    }

    public bool CanEdit => CurrentRole >= ProjectRole.Bearbeiter;
    public bool IsAdmin => CurrentRole == ProjectRole.Admin;

    // ═════════════════════════════════════════════════════════════════════════
    // Laden
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Lädt Projekte und die Daten des zuletzt gewählten Projekts (nur beim ersten Aufruf).</summary>
    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded || IsLoading) return;
        await LoadAsync();
    }

    public async Task LoadAsync(Guid? projectId = null)
    {
        await _loadLock.WaitAsync();
        IsLoading = true;
        LoadError = null;
        Notify();
        try
        {
            if (_store is LocalDataStore local && local.IsEmpty)
                await SeedData.SeedDemoAsync(_store);

            if (_client != null)
                await AcceptInvitationsAsync();

            Projects = (await _store.GetAllAsync<Project>()).OrderBy(p => p.CreatedAt).ToList();
            if (Projects.Count == 0)
            {
                var project = await SeedData.CreateEmptyProjectAsync(_store, "Privat", "Privat", _auth.UserId);
                Projects.Add(project);
            }

            var targetId = projectId ?? CurrentProject?.Id ?? _preferences.LastProjectId;
            CurrentProject = Projects.FirstOrDefault(p => p.Id == targetId) ?? Projects[0];
            _preferences.LastProjectId = CurrentProject.Id;

            await LoadProjectDataAsync(CurrentProject.Id);
            IsLoaded = true;

            if (_preferences.AutoBookRecurring && CanEdit)
                await BookDueRecurringAsync(silent: false);
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
        finally
        {
            IsLoading = false;
            _loadLock.Release();
            Notify();
        }
    }

    public Task SelectProjectAsync(Guid projectId) => LoadAsync(projectId);

    /// <summary>Verwirft alle zwischengespeicherten Daten (z. B. nach dem Abmelden).</summary>
    public void Reset()
    {
        IsLoaded = false;
        CurrentProject = null;
        Projects = new();
        Members = new();
        Accounts = new();
        Transactions = new();
        Categories = new();
        Budgets = new();
        Recurring = new();
        Goals = new();
        Notify();
    }

    private async Task LoadProjectDataAsync(Guid projectId)
    {
        var accounts = _store.GetAllAsync<Konto>(projectId);
        var transactions = _store.GetAllAsync<Transaction>(projectId);
        var categories = _store.GetAllAsync<Category>(projectId);
        var budgets = _store.GetAllAsync<Budget>(projectId);
        var recurring = _store.GetAllAsync<RecurringTransaction>(projectId);
        var goals = _store.GetAllAsync<SavingsGoal>(projectId);
        var members = _store.GetAllAsync<ProjectMember>(projectId);
        await Task.WhenAll(accounts, transactions, categories, budgets, recurring, goals, members);

        Accounts = accounts.Result.OrderBy(a => a.CreatedAt).ThenBy(a => a.Name).ToList();
        Transactions = transactions.Result.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).ToList();
        Categories = categories.Result.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
        Budgets = budgets.Result.OrderBy(b => b.Name).ToList();
        Recurring = recurring.Result.OrderBy(r => r.NextDue).ToList();
        Goals = goals.Result.OrderBy(g => g.CreatedAt).ToList();
        Members = members.Result.OrderBy(m => m.CreatedAt).ToList();
        RecalculateBalances();
    }

    private async Task AcceptInvitationsAsync()
    {
        try
        {
            await _client!.Rpc("accept_pending_invitations", null);
        }
        catch (Exception)
        {
            // Funktion optional – fehlt sie, bleiben Einladungen einfach offen.
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Lookups & Berechnungen
    // ═════════════════════════════════════════════════════════════════════════

    public Konto? GetAccount(Guid? id) => id == null ? null : Accounts.FirstOrDefault(a => a.Id == id);
    public Category? GetCategory(Guid? id) => id == null ? null : Categories.FirstOrDefault(c => c.Id == id);
    public Transaction? GetTransaction(Guid id) => Transactions.FirstOrDefault(t => t.Id == id);
    public string AccountName(Guid? id) => GetAccount(id)?.Name ?? "Unbekannt";
    public string CategoryName(Guid? id) => GetCategory(id)?.Name ?? "Ohne Kategorie";

    public IEnumerable<Konto> ActiveAccounts => Accounts.Where(a => a.IsActive);

    public decimal NetWorth => ActiveAccounts.Sum(a => a.Balance);

    /// <summary>Wirkung einer Buchung auf ein bestimmtes Konto (Vorzeichen-behaftet).</summary>
    public static decimal SignedAmountFor(Transaction t, Guid accountId)
    {
        if (t.Type == TransactionType.Umbuchung)
        {
            if (t.AccountId == accountId) return -t.Amount;
            if (t.ToAccountId == accountId) return t.Amount;
            return 0;
        }
        if (t.AccountId != accountId) return 0;
        return t.Type == TransactionType.Einnahme ? t.Amount : -t.Amount;
    }

    /// <summary>Vorzeichen aus Sicht des Gesamtvermögens (Umbuchungen sind neutral).</summary>
    public static decimal SignedAmount(Transaction t) => t.Type switch
    {
        TransactionType.Einnahme => t.Amount,
        TransactionType.Ausgabe => -t.Amount,
        _ => 0
    };

    private void RecalculateBalances()
    {
        foreach (var account in Accounts)
            account.Balance = account.OpeningBalance + Transactions.Sum(t => SignedAmountFor(t, account.Id));
    }

    public decimal BalanceAt(Guid accountId, DateTime dateInclusive)
    {
        var account = GetAccount(accountId);
        if (account == null) return 0;
        return account.OpeningBalance + Transactions
            .Where(t => t.Date.Date <= dateInclusive.Date)
            .Sum(t => SignedAmountFor(t, accountId));
    }

    public decimal NetWorthAt(DateTime dateInclusive) =>
        ActiveAccounts.Sum(a => BalanceAt(a.Id, dateInclusive));

    /// <summary>Buchungen im Zeitraum [from, to).</summary>
    public IEnumerable<Transaction> InRange(DateTime from, DateTime to) =>
        Transactions.Where(t => t.Date >= from && t.Date < to);

    public decimal Income(DateTime from, DateTime to) =>
        InRange(from, to).Where(t => t.Type == TransactionType.Einnahme).Sum(t => t.Amount);

    public decimal Expenses(DateTime from, DateTime to) =>
        InRange(from, to).Where(t => t.Type == TransactionType.Ausgabe).Sum(t => t.Amount);

    public static DateTime MonthStart(DateTime date) => new(date.Year, date.Month, 1);

    public decimal MonthIncome(DateTime month) => Income(MonthStart(month), MonthStart(month).AddMonths(1));
    public decimal MonthExpenses(DateTime month) => Expenses(MonthStart(month), MonthStart(month).AddMonths(1));

    /// <summary>Ausgaben je Kategorie im Zeitraum, absteigend sortiert. Key null = ohne Kategorie.</summary>
    public List<(Category? Category, decimal Amount)> ExpensesByCategory(DateTime from, DateTime to) =>
        InRange(from, to)
            .Where(t => t.Type == TransactionType.Ausgabe)
            .GroupBy(t => t.CategoryId)
            .Select(g => (GetCategory(g.Key), g.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Item2)
            .ToList();

    public List<(Category? Category, decimal Amount)> IncomeByCategory(DateTime from, DateTime to) =>
        InRange(from, to)
            .Where(t => t.Type == TransactionType.Einnahme)
            .GroupBy(t => t.CategoryId)
            .Select(g => (GetCategory(g.Key), g.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Item2)
            .ToList();

    /// <summary>Zeitraum [Start, End) der Budgetperiode, in der <paramref name="reference"/> liegt.</summary>
    public static (DateTime Start, DateTime End) PeriodRange(BudgetPeriod period, DateTime reference)
    {
        var day = reference.Date;
        return period switch
        {
            BudgetPeriod.Woechentlich => WeekRange(day),
            BudgetPeriod.Jaehrlich => (new DateTime(day.Year, 1, 1), new DateTime(day.Year + 1, 1, 1)),
            _ => (MonthStart(day), MonthStart(day).AddMonths(1))
        };

        static (DateTime, DateTime) WeekRange(DateTime d)
        {
            var offset = ((int)d.DayOfWeek + 6) % 7; // Montag = 0
            var start = d.AddDays(-offset);
            return (start, start.AddDays(7));
        }
    }

    public IEnumerable<Transaction> BudgetTransactions(Budget budget, DateTime reference)
    {
        var (start, end) = PeriodRange(budget.Period, reference);
        return InRange(start, end).Where(t => t.Type == TransactionType.Ausgabe &&
                                              (budget.CategoryId == null || t.CategoryId == budget.CategoryId));
    }

    public decimal BudgetSpent(Budget budget, DateTime reference) =>
        BudgetTransactions(budget, reference).Sum(t => t.Amount);

    /// <summary>Budgetlimit auf einen Monat umgerechnet (für Monatsauswertungen).</summary>
    public static decimal MonthlyLimit(Budget budget) => budget.Period switch
    {
        BudgetPeriod.Woechentlich => Math.Round(budget.Limit * 52m / 12m, 2),
        BudgetPeriod.Jaehrlich => Math.Round(budget.Limit / 12m, 2),
        _ => budget.Limit
    };

    public static DateTime NextOccurrence(RecurringTransaction r, DateTime from) => r.Interval switch
    {
        RecurrenceInterval.Woechentlich => from.AddDays(7),
        RecurrenceInterval.Quartalsweise => from.AddMonths(3),
        RecurrenceInterval.Halbjaehrlich => from.AddMonths(6),
        RecurrenceInterval.Jaehrlich => from.AddYears(1),
        _ => from.AddMonths(1)
    };

    /// <summary>Betrag eines Dauerauftrags normiert auf einen Monat.</summary>
    public static decimal MonthlyEquivalent(RecurringTransaction r) => r.Interval switch
    {
        RecurrenceInterval.Woechentlich => r.Amount * 52m / 12m,
        RecurrenceInterval.Quartalsweise => r.Amount / 3m,
        RecurrenceInterval.Halbjaehrlich => r.Amount / 6m,
        RecurrenceInterval.Jaehrlich => r.Amount / 12m,
        _ => r.Amount
    };

    /// <summary>Kategorievorschlag anhand früherer Buchungen mit ähnlicher Beschreibung.</summary>
    public Guid? SuggestCategory(string description, TransactionType type)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < 3) return null;
        var needle = description.Trim();
        return Transactions
            .Where(t => t.Type == type && t.CategoryId != null &&
                        (t.Description.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ||
                         needle.StartsWith(t.Description, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(t => t.CategoryId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Konten
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Speichert ein Konto. <paramref name="desiredBalance"/> setzt den aktuellen Kontostand, indem der
    /// Anfangssaldo entsprechend angepasst wird.
    /// </summary>
    public async Task<bool> SaveAccountAsync(Konto account, decimal? desiredBalance = null)
    {
        if (!EnsureCanEdit()) return false;
        var copy = account.Clone();
        copy.ProjectId = CurrentProject!.Id;
        if (desiredBalance.HasValue)
            copy.OpeningBalance = desiredBalance.Value - Transactions.Sum(t => SignedAmountFor(t, copy.Id));

        return await RunAsync(async () =>
        {
            await _store.UpsertAsync(copy);
            Replace(Accounts, copy);
        });
    }

    /// <summary>Löscht ein Konto inklusive aller zugehörigen Buchungen und Daueraufträge.</summary>
    public async Task<bool> DeleteAccountAsync(Guid accountId)
    {
        if (!EnsureCanEdit()) return false;
        return await RunAsync(async () =>
        {
            var txIds = Transactions.Where(t => t.AccountId == accountId || t.ToAccountId == accountId).Select(t => t.Id).ToList();
            var recIds = Recurring.Where(r => r.AccountId == accountId || r.ToAccountId == accountId).Select(r => r.Id).ToList();
            await _store.DeleteManyAsync<Transaction>(txIds);
            await _store.DeleteManyAsync<RecurringTransaction>(recIds);
            await _store.DeleteAsync<Konto>(accountId);
            Transactions.RemoveAll(t => txIds.Contains(t.Id));
            Recurring.RemoveAll(r => recIds.Contains(r.Id));
            Accounts.RemoveAll(a => a.Id == accountId);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Transaktionen
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<bool> SaveTransactionAsync(Transaction transaction)
    {
        if (!EnsureCanEdit()) return false;
        var copy = transaction.Clone();
        copy.ProjectId = CurrentProject!.Id;
        copy.Amount = Math.Abs(copy.Amount);
        if (copy.Type != TransactionType.Umbuchung) copy.ToAccountId = null;

        return await RunAsync(async () =>
        {
            await _store.UpsertAsync(copy);
            Replace(Transactions, copy);
            SortTransactions();
        });
    }

    public async Task<bool> ImportTransactionsAsync(IReadOnlyCollection<Transaction> transactions)
    {
        if (!EnsureCanEdit()) return false;
        foreach (var t in transactions) t.ProjectId = CurrentProject!.Id;
        return await RunAsync(async () =>
        {
            await _store.UpsertManyAsync(transactions);
            Transactions.AddRange(transactions);
            SortTransactions();
        });
    }

    public async Task<bool> DeleteTransactionAsync(Guid id)
    {
        if (!EnsureCanEdit()) return false;
        return await RunAsync(async () =>
        {
            await _store.DeleteAsync<Transaction>(id);
            Transactions.RemoveAll(t => t.Id == id);
        });
    }

    public async Task<bool> DeleteTransactionsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (!EnsureCanEdit()) return false;
        return await RunAsync(async () =>
        {
            await _store.DeleteManyAsync<Transaction>(ids);
            Transactions.RemoveAll(t => ids.Contains(t.Id));
        });
    }

    private void SortTransactions() =>
        Transactions = Transactions.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).ToList();

    // ═════════════════════════════════════════════════════════════════════════
    // Kategorien
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<bool> SaveCategoryAsync(Category category)
    {
        if (!EnsureCanEdit()) return false;
        var copy = category.Clone();
        copy.ProjectId = CurrentProject!.Id;
        return await RunAsync(async () =>
        {
            await _store.UpsertAsync(copy);
            Replace(Categories, copy);
            Categories = Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
        });
    }

    /// <summary>Löscht eine Kategorie; betroffene Buchungen, Budgets und Daueraufträge verlieren die Zuordnung.</summary>
    public async Task<bool> DeleteCategoryAsync(Guid categoryId)
    {
        if (!EnsureCanEdit()) return false;
        return await RunAsync(async () =>
        {
            var affectedTx = Transactions.Where(t => t.CategoryId == categoryId).ToList();
            var affectedRec = Recurring.Where(r => r.CategoryId == categoryId).ToList();
            var affectedBudgets = Budgets.Where(b => b.CategoryId == categoryId).ToList();
            affectedTx.ForEach(t => t.CategoryId = null);
            affectedRec.ForEach(r => r.CategoryId = null);
            await _store.UpsertManyAsync(affectedTx);
            await _store.UpsertManyAsync(affectedRec);
            await _store.DeleteManyAsync<Budget>(affectedBudgets.Select(b => b.Id).ToList());
            await _store.DeleteAsync<Category>(categoryId);
            Budgets.RemoveAll(b => b.CategoryId == categoryId);
            Categories.RemoveAll(c => c.Id == categoryId);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Budgets
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<bool> SaveBudgetAsync(Budget budget)
    {
        if (!EnsureCanEdit()) return false;
        var copy = budget.Clone();
        copy.ProjectId = CurrentProject!.Id;
        return await RunAsync(async () =>
        {
            await _store.UpsertAsync(copy);
            Replace(Budgets, copy);
            Budgets = Budgets.OrderBy(b => b.Name).ToList();
        });
    }

    public async Task<bool> DeleteBudgetAsync(Guid id)
    {
        if (!EnsureCanEdit()) return false;
        return await RunAsync(async () =>
        {
            await _store.DeleteAsync<Budget>(id);
            Budgets.RemoveAll(b => b.Id == id);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Daueraufträge
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<bool> SaveRecurringAsync(RecurringTransaction recurring)
    {
        if (!EnsureCanEdit()) return false;
        var copy = recurring.Clone();
        copy.ProjectId = CurrentProject!.Id;
        copy.Amount = Math.Abs(copy.Amount);
        if (copy.Type != TransactionType.Umbuchung) copy.ToAccountId = null;
        return await RunAsync(async () =>
        {
            await _store.UpsertAsync(copy);
            Replace(Recurring, copy);
            Recurring = Recurring.OrderBy(r => r.NextDue).ToList();
        });
    }

    public async Task<bool> DeleteRecurringAsync(Guid id)
    {
        if (!EnsureCanEdit()) return false;
        return await RunAsync(async () =>
        {
            await _store.DeleteAsync<RecurringTransaction>(id);
            Recurring.RemoveAll(r => r.Id == id);
        });
    }

    /// <summary>Bucht den nächsten Termin eines Dauerauftrags sofort und rückt den Termin weiter.</summary>
    public async Task<bool> BookRecurringNowAsync(Guid id)
    {
        if (!EnsureCanEdit()) return false;
        var recurring = Recurring.FirstOrDefault(r => r.Id == id);
        if (recurring == null) return false;
        return await RunAsync(async () =>
        {
            var tx = CreateFromRecurring(recurring, recurring.NextDue);
            recurring.NextDue = NextOccurrence(recurring, recurring.NextDue);
            if (recurring.EndDate.HasValue && recurring.NextDue > recurring.EndDate) recurring.IsActive = false;
            await _store.UpsertAsync(tx);
            await _store.UpsertAsync(recurring);
            Transactions.Add(tx);
            SortTransactions();
        });
    }

    /// <summary>
    /// Verbucht alle fälligen Termine aktiver Daueraufträge mit automatischer Buchung
    /// (mit <paramref name="includeManual"/> auch die manuell zu buchenden).
    /// </summary>
    public async Task<int> BookDueRecurringAsync(bool silent = true, bool includeManual = false)
    {
        if (!CanEdit) return 0;
        var today = DateTime.Today;
        var newTransactions = new List<Transaction>();
        var changed = new List<RecurringTransaction>();

        foreach (var r in Recurring.Where(r => r.IsActive && (r.AutoBook || includeManual) && r.NextDue.Date <= today))
        {
            var guard = 0;
            while (r.IsActive && r.NextDue.Date <= today && guard++ < 120)
            {
                if (r.EndDate.HasValue && r.NextDue.Date > r.EndDate.Value.Date)
                {
                    r.IsActive = false;
                    break;
                }
                newTransactions.Add(CreateFromRecurring(r, r.NextDue));
                r.NextDue = NextOccurrence(r, r.NextDue);
            }
            changed.Add(r);
        }

        if (newTransactions.Count == 0) return 0;

        var ok = await RunAsync(async () =>
        {
            await _store.UpsertManyAsync(newTransactions);
            await _store.UpsertManyAsync(changed);
            Transactions.AddRange(newTransactions);
            SortTransactions();
        });

        if (ok && !silent)
            _snackbar.Add($"{newTransactions.Count} fällige Dauerauftragsbuchung(en) wurden verbucht.", Severity.Info);
        return ok ? newTransactions.Count : 0;
    }

    private Transaction CreateFromRecurring(RecurringTransaction r, DateTime date) => new()
    {
        ProjectId = CurrentProject!.Id,
        AccountId = r.AccountId,
        ToAccountId = r.Type == TransactionType.Umbuchung ? r.ToAccountId : null,
        CategoryId = r.CategoryId,
        Description = r.Description,
        Amount = r.Amount,
        Date = date.Date,
        Type = r.Type,
        Notes = "Automatisch aus Dauerauftrag",
        RecurringId = r.Id
    };

    // ═════════════════════════════════════════════════════════════════════════
    // Sparziele
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<bool> SaveGoalAsync(SavingsGoal goal)
    {
        if (!EnsureCanEdit()) return false;
        var copy = goal.Clone();
        copy.ProjectId = CurrentProject!.Id;
        return await RunAsync(async () =>
        {
            await _store.UpsertAsync(copy);
            Replace(Goals, copy);
        });
    }

    public async Task<bool> DeleteGoalAsync(Guid id)
    {
        if (!EnsureCanEdit()) return false;
        return await RunAsync(async () =>
        {
            await _store.DeleteAsync<SavingsGoal>(id);
            Goals.RemoveAll(g => g.Id == id);
        });
    }

    /// <summary>Ein- (positiv) oder Auszahlung (negativ) auf ein Sparziel.</summary>
    public async Task<bool> ContributeToGoalAsync(Guid id, decimal amount)
    {
        var goal = Goals.FirstOrDefault(g => g.Id == id);
        if (goal == null) return false;
        var copy = goal.Clone();
        copy.CurrentAmount = Math.Max(0, copy.CurrentAmount + amount);
        return await SaveGoalAsync(copy);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Projekte & Mitglieder
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<Project?> CreateProjectAsync(string name, string icon, bool withDefaultCategories = true)
    {
        Project? created = null;
        var ok = await RunAsync(async () =>
        {
            created = withDefaultCategories
                ? await SeedData.CreateEmptyProjectAsync(_store, name, icon, _auth.UserId)
                : await SeedData.CreateProjectAsync(_store, name, icon, _auth.UserId);
            Projects.Add(created);
        });
        if (!ok || created == null) return null;
        await SelectProjectAsync(created.Id);
        return created;
    }

    /// <summary>Legt aus einem JSON-Backup ein neues Projekt an. Alle IDs werden neu vergeben.</summary>
    public async Task<Project?> ImportBackupAsNewProjectAsync(ProjectBackup backup, string name)
    {
        Project? project = null;
        var ok = await RunAsync(async () =>
        {
            project = await SeedData.CreateProjectAsync(_store, name, "Projekt", _auth.UserId);
            var ids = new Dictionary<Guid, Guid>();
            Guid Map(Guid old) => ids.TryGetValue(old, out var n) ? n : ids[old] = Guid.NewGuid();
            Guid? MapOpt(Guid? old) => old.HasValue && ids.ContainsKey(old.Value) ? ids[old.Value] : null;

            foreach (var a in backup.Accounts) { a.Id = Map(a.Id); a.ProjectId = project.Id; }
            foreach (var c in backup.Categories) { c.Id = Map(c.Id); c.ProjectId = project.Id; }
            foreach (var r in backup.Recurring)
            {
                r.Id = Map(r.Id); r.ProjectId = project.Id;
                r.AccountId = Map(r.AccountId); r.ToAccountId = MapOpt(r.ToAccountId); r.CategoryId = MapOpt(r.CategoryId);
            }
            foreach (var t in backup.Transactions)
            {
                t.Id = Guid.NewGuid(); t.ProjectId = project.Id;
                t.AccountId = Map(t.AccountId); t.ToAccountId = MapOpt(t.ToAccountId);
                t.CategoryId = MapOpt(t.CategoryId); t.RecurringId = MapOpt(t.RecurringId);
            }
            foreach (var b in backup.Budgets) { b.Id = Guid.NewGuid(); b.ProjectId = project.Id; b.CategoryId = MapOpt(b.CategoryId); }
            foreach (var g in backup.Goals) { g.Id = Guid.NewGuid(); g.ProjectId = project.Id; }

            await _store.UpsertManyAsync(backup.Accounts);
            await _store.UpsertManyAsync(backup.Categories);
            await _store.UpsertManyAsync(backup.Recurring);
            await _store.UpsertManyAsync(backup.Transactions);
            await _store.UpsertManyAsync(backup.Budgets);
            await _store.UpsertManyAsync(backup.Goals);
            Projects.Add(project);
        });
        if (!ok || project == null) return null;
        await SelectProjectAsync(project.Id);
        return project;
    }

    public async Task<bool> UpdateProjectAsync(Project project)
    {
        if (!IsAdmin) return Denied();
        return await RunAsync(async () =>
        {
            await _store.UpsertAsync(project);
            Replace(Projects, project);
            if (CurrentProject?.Id == project.Id) CurrentProject = project;
        });
    }

    /// <summary>Löscht ein Projekt samt aller Daten.</summary>
    public async Task<bool> DeleteProjectAsync(Guid projectId)
    {
        if (CurrentProject?.Id == projectId && !IsAdmin) return Denied();
        var ok = await RunAsync(async () =>
        {
            async Task Purge<T>() where T : Supabase.Postgrest.Models.BaseModel, IProjectEntity, new()
            {
                var ids = (await _store.GetAllAsync<T>(projectId)).Select(x => x.Id).ToList();
                await _store.DeleteManyAsync<T>(ids);
            }

            await Purge<Transaction>();
            await Purge<RecurringTransaction>();
            await Purge<Budget>();
            await Purge<SavingsGoal>();
            await Purge<Category>();
            await Purge<Konto>();
            await Purge<ProjectMember>();
            await _store.DeleteAsync<Project>(projectId);
            Projects.RemoveAll(p => p.Id == projectId);
        });
        if (ok && CurrentProject?.Id == projectId)
        {
            CurrentProject = null;
            _preferences.LastProjectId = null;
            await LoadAsync();
        }
        return ok;
    }

    public async Task<bool> InviteMemberAsync(string email, ProjectRole role)
    {
        if (!IsAdmin) return Denied();
        email = email.Trim().ToLowerInvariant();
        if (Members.Any(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            _snackbar.Add("Diese Person ist bereits Mitglied oder eingeladen.", Severity.Warning);
            return false;
        }
        var member = new ProjectMember { ProjectId = CurrentProject!.Id, Email = email, Role = role };
        return await RunAsync(async () =>
        {
            await _store.UpsertAsync(member);
            Members.Add(member);
        });
    }

    public async Task<bool> UpdateMemberRoleAsync(ProjectMember member, ProjectRole role)
    {
        if (!IsAdmin) return Denied();
        var previous = member.Role;
        member.Role = role;
        var ok = await RunAsync(() => _store.UpsertAsync(member));
        if (!ok) member.Role = previous;
        return ok;
    }

    public async Task<bool> RemoveMemberAsync(Guid memberId)
    {
        if (!IsAdmin) return Denied();
        return await RunAsync(async () =>
        {
            await _store.DeleteAsync<ProjectMember>(memberId);
            Members.RemoveAll(m => m.Id == memberId);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Helfer
    // ═════════════════════════════════════════════════════════════════════════

    private bool EnsureCanEdit()
    {
        if (CurrentProject == null)
        {
            _snackbar.Add("Kein Projekt ausgewählt.", Severity.Warning);
            return false;
        }
        return CanEdit || Denied();
    }

    private bool Denied()
    {
        _snackbar.Add("Dafür fehlen dir in diesem Projekt die Rechte.", Severity.Warning);
        return false;
    }

    private async Task<bool> RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            RecalculateBalances();
            Notify();
            return true;
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Speichern fehlgeschlagen: {ex.Message}", Severity.Error);
            return false;
        }
    }

    private static void Replace<T>(List<T> list, T item) where T : IEntity
    {
        var index = list.FindIndex(x => x.Id == item.Id);
        if (index >= 0) list[index] = item;
        else list.Add(item);
    }

    public void Notify() => Changed?.Invoke();
}
