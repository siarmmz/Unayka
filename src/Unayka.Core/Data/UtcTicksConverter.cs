using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Unayka.Core.Data;

public sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
