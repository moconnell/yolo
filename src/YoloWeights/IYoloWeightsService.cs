using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YoloWeights;

public interface IYoloWeightsService
{
    Task<IDictionary<string, YoloAbstractions.Weight>> GetWeightsAsync(CancellationToken cancellationToken);
}