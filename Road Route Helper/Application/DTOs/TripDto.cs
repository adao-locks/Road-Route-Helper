using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Application.DTOs
{
    public class StopDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public StopCategory Category { get; set; } = StopCategory.Custom;

        [Range(-90, 90, ErrorMessage = "A latitude deve estar entre -90 e 90.")]
        public double Latitude { get; set; }

        [Range(-180, 180, ErrorMessage = "A longitude deve estar entre -180 e 180.")]
        public double Longitude { get; set; }

        public string Notes { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public class TripDto : IValidatableObject
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Informe o nome da viagem.")]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "O nome da viagem deve ter até 120 caracteres.")]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string StartingPointName { get; set; } = string.Empty;

        [Range(-90, 90, ErrorMessage = "A latitude deve estar entre -90 e 90.")]
        public double? StartingLatitude { get; set; }

        [Range(-180, 180, ErrorMessage = "A longitude deve estar entre -180 e 180.")]
        public double? StartingLongitude { get; set; }

        public string DestinationName { get; set; } = string.Empty;

        [Range(-90, 90, ErrorMessage = "A latitude deve estar entre -90 e 90.")]
        public double? DestinationLatitude { get; set; }

        [Range(-180, 180, ErrorMessage = "A longitude deve estar entre -180 e 180.")]
        public double? DestinationLongitude { get; set; }

        public List<StopDto> Stops { get; set; } = new List<StopDto>();
        public double TotalDistanceKm { get; set; }
        public TimeSpan? EstimatedDuration { get; set; }
        public List<RoutePointDto> RouteGeometry { get; set; } = new List<RoutePointDto>();
        public string Notes { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate != null && EndDate != null && EndDate < StartDate)
            {
                yield return new ValidationResult("A data final deve ser igual ou posterior à inicial.", new[] { nameof(EndDate) });
            }
        }
    }

    public class RoutePointDto
    {
        [Range(-90, 90, ErrorMessage = "A latitude deve estar entre -90 e 90.")]
        public double Latitude { get; set; }

        [Range(-180, 180, ErrorMessage = "A longitude deve estar entre -180 e 180.")]
        public double Longitude { get; set; }
    }
}
