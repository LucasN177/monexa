using monexa.Models;
using monexa.Models.Enums;
using monexa.Services.Data;

namespace monexa.Services;

/// <summary>Standardkategorien für neue Projekte und Beispieldaten für den Demo-Modus.</summary>
public static class SeedData
{
    private static readonly (string Name, CategoryType Type, string Icon, string Color, string Description)[] DefaultCategories =
    {
        ("Gehalt", CategoryType.Einnahme, "Gehalt", "Success", "Monatliches Gehalt"),
        ("Freelance", CategoryType.Einnahme, "Geschäft", "Tertiary", "Nebenverdienst"),
        ("Sonstige Einnahmen", CategoryType.Einnahme, "Geschenk", "Info", "Erstattungen, Geschenke, Zinsen"),
        ("Lebensmittel", CategoryType.Ausgabe, "Lebensmittel", "Success", "Einkäufe im Supermarkt"),
        ("Wohnen", CategoryType.Ausgabe, "Wohnen", "Error", "Miete und Hausgeld"),
        ("Nebenkosten", CategoryType.Ausgabe, "Nebenkosten", "Warning", "Strom, Gas, Wasser"),
        ("Transport", CategoryType.Ausgabe, "Transport", "Info", "Auto, Benzin, ÖPNV"),
        ("Restaurant", CategoryType.Ausgabe, "Essen & Trinken", "Warning", "Essen gehen und Lieferdienste"),
        ("Freizeit", CategoryType.Ausgabe, "Freizeit", "Secondary", "Hobbys, Kino, Events"),
        ("Abos & Streaming", CategoryType.Ausgabe, "Streaming", "Tertiary", "Netflix, Spotify & Co."),
        ("Gesundheit", CategoryType.Ausgabe, "Gesundheit", "Info", "Apotheke, Sport, Ärzte"),
        ("Shopping", CategoryType.Ausgabe, "Shopping", "Primary", "Kleidung, Elektronik, Online-Bestellungen"),
        ("Versicherungen", CategoryType.Ausgabe, "Versicherung", "Dark", "Haftpflicht, Hausrat, KFZ"),
        ("Sparen", CategoryType.Ausgabe, "Sparen", "Success", "Sparraten und Rücklagen"),
        ("Sonstiges", CategoryType.Ausgabe, "Sonstiges", "Default", "Alles andere")
    };

    public static async Task<Project> CreateProjectAsync(IDataStore store, string name, string icon, Guid? ownerId)
    {
        var project = new Project { Name = name, Icon = icon, OwnerId = ownerId };
        await store.UpsertAsync(project);
        return project;
    }

    /// <summary>Legt ein Projekt mit Standardkategorien und einem Girokonto an.</summary>
    public static async Task<Project> CreateEmptyProjectAsync(IDataStore store, string name, string icon, Guid? ownerId)
    {
        var project = await CreateProjectAsync(store, name, icon, ownerId);
        await store.UpsertManyAsync(BuildCategories(project.Id));
        await store.UpsertAsync(new Konto
        {
            ProjectId = project.Id, Name = "Girokonto", Type = AccountType.Girokonto, BankName = "Meine Bank"
        });
        return project;
    }

    private static List<Category> BuildCategories(Guid projectId) =>
        DefaultCategories.Select((c, i) => new Category
        {
            ProjectId = projectId, Name = c.Name, Type = c.Type, Icon = c.Icon, Color = c.Color,
            Description = c.Description, SortOrder = i
        }).ToList();

    /// <summary>Erzeugt realistische Beispieldaten der letzten Monate (relativ zum heutigen Datum).</summary>
    public static async Task SeedDemoAsync(IDataStore store)
    {
        var rnd = new Random(42);
        var today = DateTime.Today;
        var start = FinanceState.MonthStart(today).AddMonths(-5);

        // ── Projekt "Privat" ────────────────────────────────────────────────
        var privat = new Project { Name = "Privat", Icon = "Privat", CreatedAt = DateTime.UtcNow.AddMonths(-6) };
        var categories = BuildCategories(privat.Id);
        Guid Cat(string name) => categories.First(c => c.Name == name).Id;

        var giro = new Konto { ProjectId = privat.Id, Name = "Hauptkonto", Type = AccountType.Girokonto, IBAN = "DE89 3704 0044 0532 0130 00", BankName = "Sparkasse", OpeningBalance = 2450, Description = "Hauptkonto für alltägliche Transaktionen", CreatedAt = DateTime.UtcNow.AddMonths(-6) };
        var spar = new Konto { ProjectId = privat.Id, Name = "Sparkonto", Type = AccountType.Sparkonto, IBAN = "DE89 3704 0044 0532 0130 01", BankName = "Sparkasse", OpeningBalance = 12000, Description = "Rücklagen für größere Anschaffungen", CreatedAt = DateTime.UtcNow.AddMonths(-6).AddMinutes(1) };
        var tagesgeld = new Konto { ProjectId = privat.Id, Name = "Tagesgeldkonto", Type = AccountType.Tagesgeld, IBAN = "DE89 5001 0517 0123 4567 89", BankName = "ING", OpeningBalance = 20000, Description = "Tagesgeld mit 3,0 % Zinsen", CreatedAt = DateTime.UtcNow.AddMonths(-6).AddMinutes(2) };
        var kreditkarte = new Konto { ProjectId = privat.Id, Name = "Kreditkarte", Type = AccountType.Kreditkarte, IBAN = "", BankName = "DKB", OpeningBalance = 0, Description = "Für Reisen und Online-Einkäufe", CreatedAt = DateTime.UtcNow.AddMonths(-6).AddMinutes(3) };
        var depot = new Konto { ProjectId = privat.Id, Name = "Depot", Type = AccountType.Depot, IBAN = "", BankName = "Trade Republic", OpeningBalance = 38500, Description = "ETF-Sparplan, langfristiger Vermögensaufbau", CreatedAt = DateTime.UtcNow.AddMonths(-6).AddMinutes(4) };
        var altkonto = new Konto { ProjectId = privat.Id, Name = "Altes Girokonto", Type = AccountType.Girokonto, IBAN = "DE89 2005 0550 1234 5678 90", BankName = "Hamburger Sparkasse", OpeningBalance = 127.85m, IsActive = false, Description = "Nicht mehr genutztes Konto", CreatedAt = DateTime.UtcNow.AddMonths(-6).AddMinutes(5) };
        var accounts = new List<Konto> { giro, spar, tagesgeld, kreditkarte, depot, altkonto };

        // Daueraufträge – NextDue wird nach dem Erzeugen der Historie gesetzt.
        var recurring = new List<RecurringTransaction>
        {
            new() { ProjectId = privat.Id, Description = "Gehalt", Amount = 3450, Type = TransactionType.Einnahme, AccountId = giro.Id, CategoryId = Cat("Gehalt"), Interval = RecurrenceInterval.Monatlich, NextDue = start.AddDays(0) },
            new() { ProjectId = privat.Id, Description = "Miete", Amount = 950, Type = TransactionType.Ausgabe, AccountId = giro.Id, CategoryId = Cat("Wohnen"), Interval = RecurrenceInterval.Monatlich, NextDue = start.AddDays(2) },
            new() { ProjectId = privat.Id, Description = "Strom & Gas", Amount = 145, Type = TransactionType.Ausgabe, AccountId = giro.Id, CategoryId = Cat("Nebenkosten"), Interval = RecurrenceInterval.Monatlich, NextDue = start.AddDays(4) },
            new() { ProjectId = privat.Id, Description = "Netflix", Amount = 13.99m, Type = TransactionType.Ausgabe, AccountId = kreditkarte.Id, CategoryId = Cat("Abos & Streaming"), Interval = RecurrenceInterval.Monatlich, NextDue = start.AddDays(5) },
            new() { ProjectId = privat.Id, Description = "Spotify", Amount = 10.99m, Type = TransactionType.Ausgabe, AccountId = kreditkarte.Id, CategoryId = Cat("Abos & Streaming"), Interval = RecurrenceInterval.Monatlich, NextDue = start.AddDays(5) },
            new() { ProjectId = privat.Id, Description = "Fitnessstudio", Amount = 39.90m, Type = TransactionType.Ausgabe, AccountId = giro.Id, CategoryId = Cat("Gesundheit"), Interval = RecurrenceInterval.Monatlich, NextDue = start.AddDays(14) },
            new() { ProjectId = privat.Id, Description = "Sparrate", Amount = 500, Type = TransactionType.Umbuchung, AccountId = giro.Id, ToAccountId = spar.Id, Interval = RecurrenceInterval.Monatlich, NextDue = start.AddDays(5) },
            new() { ProjectId = privat.Id, Description = "ETF-Sparplan", Amount = 250, Type = TransactionType.Umbuchung, AccountId = giro.Id, ToAccountId = depot.Id, Interval = RecurrenceInterval.Monatlich, NextDue = start.AddDays(14) },
            new() { ProjectId = privat.Id, Description = "Haftpflichtversicherung", Amount = 89, Type = TransactionType.Ausgabe, AccountId = giro.Id, CategoryId = Cat("Versicherungen"), Interval = RecurrenceInterval.Jaehrlich, NextDue = start.AddDays(20) },
            new() { ProjectId = privat.Id, Description = "Kreditkartenabrechnung", Amount = 500, Type = TransactionType.Umbuchung, AccountId = giro.Id, ToAccountId = kreditkarte.Id, Interval = RecurrenceInterval.Monatlich, NextDue = start.AddDays(27) }
        };

        var transactions = new List<Transaction>();
        void Add(DateTime date, string desc, decimal amount, TransactionType type, Guid account, Guid? category, Guid? to = null, string? notes = null, Guid? recurringId = null)
        {
            if (date.Date > today) return;
            transactions.Add(new Transaction
            {
                ProjectId = privat.Id, Date = date.Date, Description = desc, Amount = Math.Round(amount, 2), Type = type,
                AccountId = account, CategoryId = category, ToAccountId = to, Notes = notes, RecurringId = recurringId,
                CreatedAt = date.ToUniversalTime()
            });
        }

        // Historie der Daueraufträge
        foreach (var r in recurring)
        {
            var due = r.NextDue;
            while (due.Date <= today)
            {
                if (r.AutoBook) Add(due, r.Description, r.Amount, r.Type, r.AccountId, r.CategoryId, r.ToAccountId, "Automatisch aus Dauerauftrag", r.Id);
                due = FinanceState.NextOccurrence(r, due);
            }
            r.NextDue = due;
        }

        // Variable Ausgaben
        string[] supermarkets = { "REWE", "Aldi", "Lidl", "Edeka", "Wochenmarkt" };
        string[] restaurants = { "Vapiano", "Sushi Bar", "La Piazza", "Döner Kebab", "Lieferando" };
        string[] leisure = { "Kino", "Bowling", "Steam", "Konzertticket", "Buchhandlung" };
        string[] shops = { "Amazon", "Zara", "H&M Online", "MediaMarkt", "Deichmann" };

        for (var month = start; month <= today; month = month.AddMonths(1))
        {
            for (var week = 0; week < 5; week++)
            {
                Add(month.AddDays(week * 7 + 1), supermarkets[rnd.Next(supermarkets.Length)], 45 + rnd.Next(0, 5000) / 100m, TransactionType.Ausgabe, giro.Id, Cat("Lebensmittel"));
                if (rnd.NextDouble() < 0.6)
                    Add(month.AddDays(week * 7 + 4), supermarkets[rnd.Next(supermarkets.Length)], 12 + rnd.Next(0, 2500) / 100m, TransactionType.Ausgabe, kreditkarte.Id, Cat("Lebensmittel"));
            }

            Add(month.AddDays(3 + rnd.Next(5)), "Tankstelle", 55 + rnd.Next(0, 3000) / 100m, TransactionType.Ausgabe, giro.Id, Cat("Transport"));
            Add(month.AddDays(17 + rnd.Next(5)), "Tankstelle", 50 + rnd.Next(0, 3000) / 100m, TransactionType.Ausgabe, giro.Id, Cat("Transport"));
            Add(month.AddDays(10), "Deutschlandticket", 58, TransactionType.Ausgabe, giro.Id, Cat("Transport"));

            for (var i = 0; i < 3 + rnd.Next(3); i++)
                Add(month.AddDays(rnd.Next(0, 27)), restaurants[rnd.Next(restaurants.Length)], 12 + rnd.Next(0, 5500) / 100m, TransactionType.Ausgabe, kreditkarte.Id, Cat("Restaurant"));

            for (var i = 0; i < 2 + rnd.Next(2); i++)
                Add(month.AddDays(rnd.Next(0, 27)), leisure[rnd.Next(leisure.Length)], 9 + rnd.Next(0, 4000) / 100m, TransactionType.Ausgabe, kreditkarte.Id, Cat("Freizeit"));

            for (var i = 0; i < 1 + rnd.Next(3); i++)
                Add(month.AddDays(rnd.Next(0, 27)), shops[rnd.Next(shops.Length)], 20 + rnd.Next(0, 9000) / 100m, TransactionType.Ausgabe, kreditkarte.Id, Cat("Shopping"));

            if (rnd.NextDouble() < 0.7)
                Add(month.AddDays(rnd.Next(0, 27)), "Apotheke", 8 + rnd.Next(0, 2500) / 100m, TransactionType.Ausgabe, giro.Id, Cat("Gesundheit"));

            if (rnd.NextDouble() < 0.5)
                Add(month.AddDays(12 + rnd.Next(10)), "Freelance Projekt", 400 + rnd.Next(0, 60000) / 100m, TransactionType.Einnahme, giro.Id, Cat("Freelance"), notes: "Webdesign-Auftrag");

            Add(month.AddDays(27), "Zinsen Tagesgeld", 48 + rnd.Next(0, 500) / 100m, TransactionType.Einnahme, tagesgeld.Id, Cat("Sonstige Einnahmen"));
        }

        var budgets = new List<Budget>
        {
            new() { ProjectId = privat.Id, Name = "Lebensmittel", CategoryId = Cat("Lebensmittel"), Limit = 450, Icon = "Lebensmittel", Color = "Success" },
            new() { ProjectId = privat.Id, Name = "Transport", CategoryId = Cat("Transport"), Limit = 220, Icon = "Transport", Color = "Warning" },
            new() { ProjectId = privat.Id, Name = "Restaurant", CategoryId = Cat("Restaurant"), Limit = 150, Icon = "Essen & Trinken", Color = "Error" },
            new() { ProjectId = privat.Id, Name = "Freizeit", CategoryId = Cat("Freizeit"), Limit = 120, Icon = "Freizeit", Color = "Secondary" },
            new() { ProjectId = privat.Id, Name = "Shopping", CategoryId = Cat("Shopping"), Limit = 200, Icon = "Shopping", Color = "Primary" },
            new() { ProjectId = privat.Id, Name = "Gesundheit", CategoryId = Cat("Gesundheit"), Limit = 80, Icon = "Gesundheit", Color = "Info" },
            new() { ProjectId = privat.Id, Name = "Gesamtausgaben", CategoryId = null, Limit = 2600, Icon = "Sonstiges", Color = "Dark" }
        };

        var goals = new List<SavingsGoal>
        {
            new() { ProjectId = privat.Id, Name = "Sommerurlaub", TargetAmount = 2500, CurrentAmount = 1350, TargetDate = today.AddMonths(7), Icon = "Urlaub", Color = "Info", CreatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { ProjectId = privat.Id, Name = "Notgroschen", TargetAmount = 10000, CurrentAmount = 7200, Icon = "Notgroschen", Color = "Success", Notes = "3 Monatsgehälter", CreatedAt = DateTime.UtcNow.AddMonths(-4).AddMinutes(1) },
            new() { ProjectId = privat.Id, Name = "Neues Auto", TargetAmount = 15000, CurrentAmount = 3100, TargetDate = today.AddMonths(30), Icon = "Auto", Color = "Warning", CreatedAt = DateTime.UtcNow.AddMonths(-4).AddMinutes(2) }
        };

        // ── Projekt "Geschäftlich" ─────────────────────────────────────────
        var business = new Project { Name = "Geschäftlich", Icon = "Geschäftlich", CreatedAt = DateTime.UtcNow.AddMonths(-5) };
        var bizCategories = new List<Category>
        {
            new() { ProjectId = business.Id, Name = "Umsatz", Type = CategoryType.Einnahme, Icon = "Geschäft", Color = "Success", SortOrder = 0 },
            new() { ProjectId = business.Id, Name = "Software", Type = CategoryType.Ausgabe, Icon = "Elektronik", Color = "Primary", SortOrder = 1 },
            new() { ProjectId = business.Id, Name = "Büro", Type = CategoryType.Ausgabe, Icon = "Wohnen", Color = "Warning", SortOrder = 2 },
            new() { ProjectId = business.Id, Name = "Steuern", Type = CategoryType.Ausgabe, Icon = "Steuern", Color = "Error", SortOrder = 3 }
        };
        var bizAccount = new Konto { ProjectId = business.Id, Name = "Geschäftskonto", Type = AccountType.Girokonto, BankName = "Qonto", OpeningBalance = 5000, IBAN = "DE12 1001 1001 2620 1234 56" };
        var bizTax = new Konto { ProjectId = business.Id, Name = "Steuerrücklage", Type = AccountType.Tagesgeld, BankName = "Qonto", OpeningBalance = 2000 };
        var bizTransactions = new List<Transaction>();
        for (var month = start; month <= today; month = month.AddMonths(1))
        {
            void AddBiz(int day, string desc, decimal amount, TransactionType type, Guid? category, Guid? to = null)
            {
                var date = month.AddDays(day);
                if (date > today) return;
                bizTransactions.Add(new Transaction { ProjectId = business.Id, Date = date, Description = desc, Amount = amount, Type = type, AccountId = bizAccount.Id, CategoryId = category, ToAccountId = to });
            }

            AddBiz(4, "Rechnung Kunde A", 2400 + rnd.Next(0, 800), TransactionType.Einnahme, bizCategories[0].Id);
            AddBiz(18, "Rechnung Kunde B", 1200 + rnd.Next(0, 1500), TransactionType.Einnahme, bizCategories[0].Id);
            AddBiz(2, "JetBrains & GitHub", 42.50m, TransactionType.Ausgabe, bizCategories[1].Id);
            AddBiz(1, "Coworking Space", 290, TransactionType.Ausgabe, bizCategories[2].Id);
            AddBiz(20, "Rücklage Steuern", 900, TransactionType.Umbuchung, null, bizTax.Id);
        }

        await store.UpsertManyAsync(new List<Project> { privat, business });
        await store.UpsertManyAsync(categories.Concat(bizCategories).ToList());
        await store.UpsertManyAsync(accounts.Append(bizAccount).Append(bizTax).ToList());
        await store.UpsertManyAsync(transactions.Concat(bizTransactions).ToList());
        await store.UpsertManyAsync(recurring);
        await store.UpsertManyAsync(budgets);
        await store.UpsertManyAsync(goals);
    }
}
