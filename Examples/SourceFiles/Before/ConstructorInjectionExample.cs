using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Examples.ConstructorInjection;

/// <summary>
/// Example: Processor with methods that take repeated dependency parameters.
/// Refactoring: constructor-injection to convert parameters to constructor-injected fields.
/// </summary>
public class NotificationProcessor
{
    // Methods receive dependencies as parameters - these should be injected via constructor
    public async Task<bool> SendEmailAsync(
        string to,
        string subject,
        string body,
        ISmtpClient smtpClient,
        ITemplateEngine templates,
        EmailConfiguration config)
    {
        var message = new EmailMessage
        {
            From = config.DefaultFromAddress,
            To = to,
            Subject = subject,
            Body = await templates.RenderAsync("email-layout", new { Content = body })
        };

        return await smtpClient.SendAsync(message);
    }

    public async Task<bool> SendTemplatedEmailAsync<T>(
        string to,
        string templateName,
        T model,
        ISmtpClient smtpClient,
        ITemplateEngine templates,
        EmailConfiguration config)
    {
        var subject = await templates.RenderAsync($"{templateName}-subject", model);
        var body = await templates.RenderAsync($"{templateName}-body", model);

        return await SendEmailAsync(to, subject, body, smtpClient, templates, config);
    }

    public async Task<BulkSendResult> SendBulkAsync(
        IEnumerable<string> recipients,
        string subject,
        string body,
        ISmtpClient smtpClient,
        ITemplateEngine templates,
        EmailConfiguration config,
        int batchSize = 50)
    {
        var result = new BulkSendResult();
        var batches = recipients.Chunk(batchSize);

        foreach (var batch in batches)
        {
            foreach (var to in batch)
            {
                var success = await SendEmailAsync(to, subject, body, smtpClient, templates, config);
                if (success)
                    result.Successful++;
                else
                    result.Failed++;
            }
        }

        return result;
    }
}

// Supporting types
public class EmailMessage
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}

public class EmailConfiguration
{
    public string DefaultFromAddress { get; set; } = "";
}

public class BulkSendResult
{
    public int Successful { get; set; }
    public int Failed { get; set; }
}

public interface ISmtpClient
{
    Task<bool> SendAsync(EmailMessage message);
}

public interface ITemplateEngine
{
    Task<string> RenderAsync<T>(string templateName, T model);
}
