using System.Text;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

public enum ToUnityBridgeResponseStatus
{
    Ok,
    InvalidJson,
    EmptyCommand,
    UnknownCommand,
    AssetNotFound,
    TypeNotResolved,
    Timeout,
    Error
}

public sealed class ToUnityBridgeResponse
{
    public ToUnityBridgeResponse(ToUnityBridgeResponseStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public ToUnityBridgeResponseStatus Status { get; }
    public string Message { get; }
    public bool IsSuccess => Status == ToUnityBridgeResponseStatus.Ok ||
                             Status == ToUnityBridgeResponseStatus.TypeNotResolved;
}

public static class ToUnityBridgeProtocol
{
    public const int ProtocolVersion = 1;

    public static string BuildJsonCommandLine(string commandName, string payload, string replyPipeName)
    {
        return $"{{\"version\":{ProtocolVersion},\"command\":\"{EscapeJsonString(commandName)}\",\"payload\":\"{EscapeJsonString(payload)}\",\"replyPipe\":\"{EscapeJsonString(replyPipeName)}\"}}\n";
    }

    public static ToUnityBridgeResponse ParseResponse(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return new ToUnityBridgeResponse(ToUnityBridgeResponseStatus.Error, "Unity bridge returned an empty response.");
        }

        var statusText = ExtractJsonString(responseJson, "status");
        var message = ExtractJsonString(responseJson, "message") ?? responseJson;

        if (statusText != null && TryParseStatus(statusText, out var status))
        {
            return new ToUnityBridgeResponse(status, message);
        }

        return new ToUnityBridgeResponse(ToUnityBridgeResponseStatus.Error,
            $"Unity bridge returned an unknown response: {responseJson}");
    }

    private static bool TryParseStatus(string statusText, out ToUnityBridgeResponseStatus status)
    {
        switch (statusText)
        {
            case nameof(ToUnityBridgeResponseStatus.Ok):
                status = ToUnityBridgeResponseStatus.Ok;
                return true;
            case nameof(ToUnityBridgeResponseStatus.InvalidJson):
                status = ToUnityBridgeResponseStatus.InvalidJson;
                return true;
            case nameof(ToUnityBridgeResponseStatus.EmptyCommand):
                status = ToUnityBridgeResponseStatus.EmptyCommand;
                return true;
            case nameof(ToUnityBridgeResponseStatus.UnknownCommand):
                status = ToUnityBridgeResponseStatus.UnknownCommand;
                return true;
            case nameof(ToUnityBridgeResponseStatus.AssetNotFound):
                status = ToUnityBridgeResponseStatus.AssetNotFound;
                return true;
            case nameof(ToUnityBridgeResponseStatus.TypeNotResolved):
                status = ToUnityBridgeResponseStatus.TypeNotResolved;
                return true;
            case nameof(ToUnityBridgeResponseStatus.Timeout):
                status = ToUnityBridgeResponseStatus.Timeout;
                return true;
            case nameof(ToUnityBridgeResponseStatus.Error):
                status = ToUnityBridgeResponseStatus.Error;
                return true;
            default:
                status = ToUnityBridgeResponseStatus.Error;
                return false;
        }
    }

    private static string ExtractJsonString(string json, string propertyName)
    {
        var propertyMarker = $"\"{propertyName}\"";
        var propertyIndex = json.IndexOf(propertyMarker, System.StringComparison.Ordinal);
        if (propertyIndex < 0)
        {
            return null;
        }

        var colonIndex = json.IndexOf(':', propertyIndex + propertyMarker.Length);
        if (colonIndex < 0)
        {
            return null;
        }

        var quoteIndex = json.IndexOf('"', colonIndex + 1);
        if (quoteIndex < 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var index = quoteIndex + 1; index < json.Length; index++)
        {
            var character = json[index];
            if (character == '"')
            {
                return builder.ToString();
            }

            if (character == '\\' && index + 1 < json.Length)
            {
                index++;
                AppendEscapedCharacter(builder, json[index]);
                continue;
            }

            builder.Append(character);
        }

        return null;
    }

    private static void AppendEscapedCharacter(StringBuilder builder, char escapedCharacter)
    {
        switch (escapedCharacter)
        {
            case '"':
                builder.Append('"');
                break;
            case '\\':
                builder.Append('\\');
                break;
            case '/':
                builder.Append('/');
                break;
            case 'b':
                builder.Append('\b');
                break;
            case 'f':
                builder.Append('\f');
                break;
            case 'n':
                builder.Append('\n');
                break;
            case 'r':
                builder.Append('\r');
                break;
            case 't':
                builder.Append('\t');
                break;
            default:
                builder.Append(escapedCharacter);
                break;
        }
    }

    private static string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\b':
                    builder.Append(@"\b");
                    break;
                case '\f':
                    builder.Append(@"\f");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
