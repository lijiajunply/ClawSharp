using System.Text.Json;
using ClawSharp.Lib.Configuration;
using MailKit.Net.Smtp;
using MimeKit;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// 电子邮件发送工具。
/// </summary>
public sealed class EmailSendTool(ClawOptions options) : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "email_send",
        "Send an email to a specified recipient.",
        ToolSecurity.Json(new
        {
            type = "object",
            properties = new
            {
                to = new { type = "string", description = "Recipient email address." },
                subject = new { type = "string", description = "Email subject." },
                body = new { type = "string", description = "Email body content (plain text)." },
                isHtml = new { type = "boolean", description = "Whether the body is HTML.", @default = false }
            },
            required = new[] { "to", "subject", "body" }
        }),
        null,
        ToolCapability.EmailSend);

    /// <inheritdoc />
    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var to = arguments.GetProperty("to").GetString() ?? string.Empty;
        var subject = arguments.GetProperty("subject").GetString() ?? string.Empty;
        var body = arguments.GetProperty("body").GetString() ?? string.Empty;
        var isHtml = arguments.TryGetProperty("isHtml", out var isHtmlEl) && isHtmlEl.GetBoolean();

        // 1. 检查收件人白名单
        var check = ToolSecurity.EnsureEmailRecipientAllowed(to, context.Permissions.EffectiveEmailRecipients);
        if (!check.IsSuccess)
        {
            return ToolSecurity.CreateApprovalOrDenied(Definition, context, check.Error!, new { to, subject });
        }

        // 2. 如果全局配置要求审批，或者权限集里显式要求
        // 注意：由于邮件发送是高危操作，即便在白名单内，如果 WorkspacePolicy 开启了 ApprovalRequired，也应当拦截
        // 这里依赖 ToolSecurity.CreateApprovalOrDenied 内部逻辑，它会检查 context.Permissions.ApprovalRequired
        if (context.Permissions.ApprovalRequired)
        {
            return ToolInvocationResult.RequiresApproval(Definition.Name, ToolSecurity.Json(new { to, subject, body_preview = body.Length > 100 ? body[..100] + "..." : body }));
        }

        // 3. 执行发送
        var emailConfig = options.Tools.Email;
        if (string.IsNullOrEmpty(emailConfig.SmtpHost))
        {
            return ToolInvocationResult.Failure(Definition.Name, "SMTP configuration is missing (SmtpHost).");
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(emailConfig.FromName, emailConfig.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            message.Body = new TextPart(isHtml ? "html" : "plain")
            {
                Text = body
            };

            using var client = new SmtpClient();
            // 在某些开发环境下可能需要忽略 SSL 证书验证错误，但生产环境不建议
            // client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            await client.ConnectAsync(emailConfig.SmtpHost, emailConfig.SmtpPort, emailConfig.EnableSsl, context.CancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(emailConfig.UserName))
            {
                await client.AuthenticateAsync(emailConfig.UserName, emailConfig.Password, context.CancellationToken).ConfigureAwait(false);
            }

            await client.SendAsync(message, context.CancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, context.CancellationToken).ConfigureAwait(false);

            return ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { status = "sent", to }));
        }
        catch (Exception ex)
        {
            return ToolInvocationResult.Failure(Definition.Name, $"Failed to send email: {ex.Message}");
        }
    }
}
