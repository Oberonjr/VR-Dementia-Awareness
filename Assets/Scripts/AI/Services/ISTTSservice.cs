using System.Threading.Tasks;

namespace VRD_AIProviders
{
    public interface ISTTService
    {
        Task<TranscriptionResponse> TranscribeAsync(TranscriptionRequest request);
    }
}
