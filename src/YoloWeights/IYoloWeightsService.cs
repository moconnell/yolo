using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YoloWeights;

public interface IYoloWeightsService
{
    Task<IReadOnlyDictionary<string, YoloAbstractions.Weight>> GetWeightsAsync(CancellationToken cancellationToken);
}