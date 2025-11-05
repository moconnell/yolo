using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RobotWealth.Api.Data;

namespace RobotWealth.Api.Interfaces;

public interface IRobotWealthApiService
{
    Task<IReadOnlyList<RwWeight>> GetWeightsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RwVolatility>> GetVolatilitiesAsync(CancellationToken cancellationToken = default);
}