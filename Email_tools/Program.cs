using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Spectre.Console;

#region Models
public class BroadcastRecipient
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public class SmtpSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = ""; // Gmail App Password (16 chars)
    public string FromName { get; set; } = "Broadcast Bot";
}

public class BroadcastConfig
{
    public string RecipientsFile { get; set; } = "recipients.json";
    public string DefaultSubject { get; set; } = "Notification";
    public int DelayBetweenEmailsMs { get; set; } = 400;
    public int MaxRetry { get; set; } = 2;
    public bool IsBodyHtml { get; set; } = false;
}

public class AppSettings
{
    public SmtpSettings Email { get; set; } = new();
    public BroadcastConfig Broadcast { get; set; } = new();
}
#endregion

class Program
{
    static async Task<int> Main()
    {
        Console.Title = "Bulk Email Tool";

        AnsiConsole.Write(
            new FigletText("Bulk Mailer")
                .Centered()
                .Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[grey]Console tool • Manage recipients • Broadcast email[/]");
        Console.WriteLine();

        // Load appsettings.json WITHOUT Microsoft.Extensions.Configuration.*
        var settings = LoadSettings("appsettings.json");
        if (string.IsNullOrWhiteSpace(settings.Email.User) ||
            string.IsNullOrWhiteSpace(settings.Email.Password))
        {
            AnsiConsole.MarkupLine("[red]Missing Gmail credentials in appsettings.json (Email.User, Email.Password).[/]");
            return 1;
        }

        // Load recipients
        var recipients = LoadRecipients(settings.Broadcast.RecipientsFile);

        // Main menu
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Choose an option[/]:")
                    .AddChoices(
                        "1) Manage recipients (JSON)",
                        "2) Compose & send broadcast",
                        "3) Placeholder demo",
                        "4) Exit"));

            if (choice.StartsWith("1"))
                await ManageRecipientsMenu(recipients, settings.Broadcast.RecipientsFile);
            else if (choice.StartsWith("2"))
                await SendBroadcastMenu(settings.Email, settings.Broadcast, recipients);
            else if (choice.StartsWith("3"))
                DemoPersonalization(recipients);
            else
                break;
        }

        AnsiConsole.MarkupLine("[green]Goodbye![/]");
        return 0;
    }

    // ---------- settings ----------
    static AppSettings LoadSettings(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new AppSettings();
            string json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to read {path}:[/] {ex.Message}");
            return new AppSettings();
        }
    }

    // ---------- recipients ----------
    static List<BroadcastRecipient> LoadRecipients(string file)
    {
        try
        {
            if (!File.Exists(file)) return new List<BroadcastRecipient>();
            var json = File.ReadAllText(file, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<BroadcastRecipient>>(json) ?? new List<BroadcastRecipient>();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Load recipients error:[/] {ex.Message}");
            return new List<BroadcastRecipient>();
        }
    }

    static bool SaveRecipients(string file, List<BroadcastRecipient> list)
    {
        try
        {
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Save recipients error:[/] {ex.Message}");
            return false;
        }
    }

    static async Task ManageRecipientsMenu(List<BroadcastRecipient> recipients, string file)
    {
        while (true)
        {
            AnsiConsole.Write(new Rule("[yellow]Recipients[/]"));
            ShowRecipientsTable(recipients);

            var sel = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Pick an action:")
                    .AddChoices("Add", "Remove", "Save", "Back"));

            if (sel == "Add")
            {
                while (true)
                {
                    var name = AnsiConsole.Prompt(
                        new TextPrompt<string>("[green]Name[/] (Enter to stop):").AllowEmpty());
                    if (string.IsNullOrWhiteSpace(name)) break;

                    var email = AnsiConsole.Prompt(
                        new TextPrompt<string>("[green]Email[/]:")
                            .Validate(input =>
                            {
                                var v = ValidateEmail(input);
                                return v.ok ? ValidationResult.Success()
                                            : ValidationResult.Error($"Invalid email: {v.message}");
                            }));

                    recipients.Add(new BroadcastRecipient { Name = name, Email = email });
                    AnsiConsole.MarkupLine($"[green]+[/] Added [bold]{name}[/] <{email}>");
                }
            }
            else if (sel == "Remove")
            {
                if (recipients.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]List is empty[/]");
                    continue;
                }

                var display = recipients
                    .Select((r, i) => $"{i + 1}. {r.Name} <{r.Email}>")
                    .ToList();

                var pick = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<string>()
                        .Title("Select recipients to remove (Space to toggle, Enter to confirm)")
                        .PageSize(10)
                        .AddChoices(display));

                if (pick.Count == 0) continue;

                var indices = pick.Select(p => display.IndexOf(p))
                                  .Where(i => i >= 0)
                                  .OrderByDescending(i => i)
                                  .ToList();

                foreach (var idx in indices) recipients.RemoveAt(idx);
                AnsiConsole.MarkupLine($"[yellow]-[/] Removed {indices.Count} item(s).");
            }
            else if (sel == "Save")
            {
                if (SaveRecipients(file, recipients))
                    AnsiConsole.MarkupLine($"[green]Saved[/] to {file}");
            }
            else // Back
            {
                if (AnsiConsole.Confirm("Save changes before going back?", true))
                {
                    if (SaveRecipients(file, recipients))
                        AnsiConsole.MarkupLine($"[green]Saved[/] to {file}");
                }
                break;
            }
        }
    }

    static void ShowRecipientsTable(List<BroadcastRecipient> recipients)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold]#[/]");
        table.AddColumn("[bold]Name[/]");
        table.AddColumn("[bold]Email[/]");

        if (recipients.Count == 0)
            table.AddRow("-", "[grey]Empty[/]", "-");
        else
            for (int i = 0; i < recipients.Count; i++)
                table.AddRow((i + 1).ToString(), Escape(recipients[i].Name), Escape(recipients[i].Email));

        AnsiConsole.Write(table);
    }

    // ---------- broadcast ----------
    static async Task SendBroadcastMenu(SmtpSettings smtp, BroadcastConfig bc, List<BroadcastRecipient> recipients)
    {
        if (recipients.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Recipient list is empty[/]. Go to option 1 to add recipients first.");
            return;
        }

        AnsiConsole.Write(new Rule("[green]Compose[/]"));
        var subject = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Subject[/]:")
                .DefaultValue(bc.DefaultSubject)
                .AllowEmpty());

        // Body source
        var bodySource = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose body source:")
                .AddChoices("Type here", "Load from file (.txt / .html)"));

        string body;
        bool isHtml = bc.IsBodyHtml;

        if (bodySource.StartsWith("Load"))
        {
            var path = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter path to body file:")
                    .Validate(p =>
                    {
                        if (string.IsNullOrWhiteSpace(p)) return ValidationResult.Error("Path is required");
                        if (!File.Exists(p)) return ValidationResult.Error("File not found");
                        return ValidationResult.Success();
                    }));

            body = await File.ReadAllTextAsync(path, Encoding.UTF8);

            // auto-detect by extension; allow override
            isHtml = path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                  || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);

            var askHtml = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Send as HTML?")
                    .AddChoices("Auto", "Yes (force HTML)", "No (plain text)"));

            if (askHtml.StartsWith("Yes")) isHtml = true;
            else if (askHtml.StartsWith("No")) isHtml = false;

            AnsiConsole.MarkupLine($"Loaded body: [green]{body.Length}[/] chars. HTML=[green]{isHtml}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Type your message. Finish by entering a single dot '.' on a new line.[/]");
            var sb = new StringBuilder();
            while (true)
            {
                var line = Console.ReadLine();
                if (line != null && line.Trim() == ".") break;
                sb.AppendLine(line);
            }
            body = sb.ToString();
            isHtml = AnsiConsole.Confirm("Send as [green]HTML[/]?", isHtml);
        }

        // Recipients: all or subset
        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Send to:")
                .AddChoices("All recipients", "Pick a subset"));

        var sendList = recipients;
        if (mode.StartsWith("Pick"))
        {
            var display = recipients.Select(r => $"{r.Name} <{r.Email}>").ToList();

            var selected = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Select recipients")
                    .PageSize(12)
                    .MoreChoicesText("[grey](Use Up/Down to see more)[/]")
                    .InstructionsText("[grey](Space to toggle, Enter to confirm)[/]")
                    .AddChoices(display));

            sendList = new List<BroadcastRecipient>();
            foreach (var s in selected)
            {
                var idx = display.IndexOf(s);
                if (idx >= 0) sendList.Add(recipients[idx]);
            }

            if (sendList.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No selection. Aborted.[/]");
                return;
            }
        }

        // Preview
        AnsiConsole.Write(new Rule("[green]Preview[/]"));
        var panel = new Panel(
            $"[bold]Subject[/]: {Escape(subject)}\n" +
            $"[bold]Body (first 300 chars)[/]:\n{EscapePreview(body, 300)}\n" +
            $"[bold]HTML[/]: {isHtml}\n" +
            $"[bold]Recipients[/]: {sendList.Count}\n" +
            $"[bold]Delay[/]: {bc.DelayBetweenEmailsMs} ms, [bold]MaxRetry[/]: {bc.MaxRetry}"
        ).Border(BoxBorder.Rounded);
        AnsiConsole.Write(panel);

        if (!AnsiConsole.Confirm("Proceed to send?", true))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return;
        }

        // Send
        var results = new List<(BroadcastRecipient r, bool ok, string msg)>();
        await AnsiConsole.Progress()
            .AutoClear(true)
            .StartAsync(async ctx =>
            {
                var t = ctx.AddTask("[green]Sending...[/]", maxValue: sendList.Count);
                foreach (var r in sendList)
                {
                    string personalized = body.Replace("{{name}}", r.Name ?? "", StringComparison.OrdinalIgnoreCase);
                    var (ok, message) = await SendMailWithRetry(
                        settings: smtp,
                        to: r.Email,
                        subject: subject,
                        body: personalized,
                        isHtml: isHtml,
                        maxRetry: bc.MaxRetry);

                    results.Add((r, ok, message));
                    t.Increment(1);
                    await Task.Delay(bc.DelayBetweenEmailsMs);
                }
            });

        // Summary
        int okc = 0, failc = 0;
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold]Name[/]");
        table.AddColumn("[bold]Email[/]");
        table.AddColumn("[bold]Status[/]");
        table.AddColumn("[bold]Message[/]");

        foreach (var res in results)
        {
            if (res.ok) okc++; else failc++;
            table.AddRow(
                Escape(res.r.Name),
                Escape(res.r.Email),
                res.ok ? "[green]Success[/]" : "[red]Failed[/]",
                Escape(res.msg));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[bold]Done[/]. Success=[green]{okc}[/], Failed=[red]{failc}[/].");
    }

    static (bool ok, string message) ValidateEmail(string email)
    {
        try { _ = new MailAddress(email); return (true, ""); }
        catch (Exception e) { return (false, e.Message); }
    }

    static async Task<(bool ok, string message)> SendMailWithRetry(
        SmtpSettings settings, string to, string subject, string body, bool isHtml, int maxRetry)
    {
        var v = ValidateEmail(to);
        if (!v.ok) return (false, $"Invalid email: {v.message}");

        for (int attempt = 1; attempt <= Math.Max(1, maxRetry); attempt++)
        {
            try
            {
                using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
                {
                    EnableSsl = settings.UseSsl,
                    Credentials = new NetworkCredential(settings.User, settings.Password),
                    Timeout = 30000
                };

                var msg = new MailMessage
                {
                    From = new MailAddress(settings.User,
                        string.IsNullOrWhiteSpace(settings.FromName) ? null : settings.FromName),
                    Subject = subject,
                    Body = body,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8,
                    IsBodyHtml = isHtml
                };
                msg.To.Add(to);

                await client.SendMailAsync(msg);
                return (true, "OK");
            }
            catch (Exception ex)
            {
                if (attempt == maxRetry)
                    return (false, ex.Message);
                await Task.Delay(1000 * attempt);
            }
        }
        return (false, "Unknown error");
    }

    // ---------- utils ----------
    static string Escape(string s) => s?.Replace("[", "[[").Replace("]", "]]") ?? "";
    static string EscapePreview(string s, int max)
    {
        var cut = s.Length > max ? s.Substring(0, max) + "..." : s;
        return Escape(cut);
    }

    static void DemoPersonalization(List<BroadcastRecipient> recipients)
    {
        AnsiConsole.Write(new Rule("[green]Placeholder demo[/]"));
        var sample = "Hi {{name}},\nToday's PnL: +3%. Monthly PnL: +12%.\n— Desk";
        var name = recipients.Count > 0 ? recipients[0].Name : "Friend";
        var output = sample.Replace("{{name}}", name, StringComparison.OrdinalIgnoreCase);

        var panel = new Panel(output).Header("Preview with {{name}}").Border(BoxBorder.Rounded);
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[grey]Tip: Use {{name}} in the body to personalize messages.[/]");
    }
}
