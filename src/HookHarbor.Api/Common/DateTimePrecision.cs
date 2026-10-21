namespace HookHarbor.Api.Common;

public static class DateTimePrecision
{
    public static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = 10;
        return value.AddTicks(-(value.Ticks % ticksPerMicrosecond));
    }
}
