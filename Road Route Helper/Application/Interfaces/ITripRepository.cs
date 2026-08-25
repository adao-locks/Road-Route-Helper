using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Application.Interfaces
{
    public interface ITripRepository
    {
        Task<IEnumerable<Trip>> GetAllAsync();
        Task<Trip?> GetByIdAsync(Guid id);
        Task<Trip> CreateAsync(Trip trip);
        Task<Trip?> UpdateAsync(Trip trip);
        Task<bool> DeleteAsync(Guid id);
        Task<Trip?> DuplicateAsync(Guid id);
    }
}
