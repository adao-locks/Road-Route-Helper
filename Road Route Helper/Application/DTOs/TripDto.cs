using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Application.DTOs
{
    public class StopDto
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Informe o nome da parada.")]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "O nome da parada deve ter até 120 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [EnumDataType(typeof(StopCategory), ErrorMessage = "A categoria da parada é inválida.")]
        public StopCategory Category { get; set; } = StopCategory.Custom;

        [Range(-90, 90, ErrorMessage = "A latitude deve estar entre -90 e 90.")]
        public double Latitude { get; set; }

        [Range(-180, 180, ErrorMessage = "A longitude deve estar entre -180 e 180.")]
        public double Longitude { get; set; }

        [Required(AllowEmptyStrings = true, ErrorMessage = "As notas da parada não podem ser nulas.")]
        public string Notes { get; set; } = string.Empty;

        public int Order { get; set; }
    }

    public class TripDto : IValidatableObject
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Informe o nome da viagem.")]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "O nome da viagem deve ter até 120 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = true, ErrorMessage = "A descrição não pode ser nula.")]
        public string Description { get; set; } = string.Empty;

        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }

        [Required(ErrorMessage = "A lista de paradas não pode ser nula.")]
        public List<StopDto> Stops { get; set; } = new List<StopDto>();

        public double TotalDistanceKm { get; set; }
        public TimeSpan? EstimatedDuration { get; set; }

        [Required(ErrorMessage = "O traçado da rota não pode ser nulo.")]
        public List<RoutePointDto> RouteGeometry { get; set; } = new List<RoutePointDto>();

        [Required(AllowEmptyStrings = true, ErrorMessage = "As notas da viagem não podem ser nulas.")]
        public string Notes { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate != null && EndDate != null && EndDate < StartDate)
            {
                yield return new ValidationResult("A data final deve ser igual ou posterior à inicial.", new[] { nameof(EndDate) });
            }
            if (Stops != null && Stops.Any(s => s == null))
            {
                yield return new ValidationResult("A lista de paradas contém um item inválido.", new[] { nameof(Stops) });
            }
            if (RouteGeometry != null && RouteGeometry.Any(p => p == null))
            {
                yield return new ValidationResult("O traçado da rota contém um ponto inválido.", new[] { nameof(RouteGeometry) });
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
