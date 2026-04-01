using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Tools;
using Xunit;

namespace ClawSharp.Lib.Tests;

public class EmailToolTests
{
    [Fact]
    public async Task EmailSendTool_ShouldRespectRecipientWhitelist()
    {
        // Arrange
        var options = new ClawOptions();
        options.Tools.Email.SmtpHost = "smtp.example.com";
        options.Tools.Email.FromAddress = "bot@example.com";
        
        var tool = new EmailSendTool(options);
        
        var permissions = new ToolPermissionSet(
            ToolCapability.EmailSend,
            [], [], [],
            ApprovalRequired: false,
            AllowedEmailRecipients: ["@trusted.com", "boss@work.com"]
        );
        
        var context = new ToolExecutionContext(
            "root", "agent", "session", "turn", "msg",
            permissions, "trace", CancellationToken.None
        );

        // Act - Allowed recipient (exact)
        var args1 = JsonSerializer.SerializeToElement(new { to = "boss@work.com", subject = "Hi", body = "Hello" });
        // Since we can't easily mock the SmtpClient without more refactoring, 
        // we expect it to fail at connection phase if host is invalid, 
        // but it should PASS the permission check.
        var result1 = await tool.ExecuteAsync(context, args1);
        
        // Act - Allowed recipient (domain)
        var args2 = JsonSerializer.SerializeToElement(new { to = "user@trusted.com", subject = "Hi", body = "Hello" });
        var result2 = await tool.ExecuteAsync(context, args2);
        
        // Act - Denied recipient
        var args3 = JsonSerializer.SerializeToElement(new { to = "attacker@evil.com", subject = "Hi", body = "Hello" });
        var result3 = await tool.ExecuteAsync(context, args3);

        // Assert
        Assert.NotEqual(ToolInvocationStatus.Denied, result1.Status); // Should proceed to execution (and likely fail due to SMTP)
        Assert.NotEqual(ToolInvocationStatus.Denied, result2.Status); // Should proceed
        Assert.Equal(ToolInvocationStatus.Denied, result3.Status);   // Should be denied by security
        Assert.Contains("Email recipient denied", result3.Error);
    }

    [Fact]
    public async Task EmailSendTool_ShouldRequireApproval_WhenEnabled()
    {
        // Arrange
        var options = new ClawOptions();
        var tool = new EmailSendTool(options);
        
        var permissions = new ToolPermissionSet(
            ToolCapability.EmailSend,
            [], [], [],
            ApprovalRequired: true,
            AllowedEmailRecipients: [] // No restriction, but approval required
        );
        
        var context = new ToolExecutionContext(
            "root", "agent", "session", "turn", "msg",
            permissions, "trace", CancellationToken.None
        );

        // Act
        var args = JsonSerializer.SerializeToElement(new { to = "anyone@anywhere.com", subject = "Hi", body = "Hello" });
        var result = await tool.ExecuteAsync(context, args);

        // Assert
        Assert.Equal(ToolInvocationStatus.ApprovalRequired, result.Status);
    }
}
