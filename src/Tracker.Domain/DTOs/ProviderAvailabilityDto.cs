namespace Tracker.Domain.DTOs;

public record ProviderAvailabilityDto(
    string Name,
    bool Available,
    string Message
);

public record AnalysisProvidersDto(
    string DefaultProvider,
    List<ProviderAvailabilityDto> Providers
);
