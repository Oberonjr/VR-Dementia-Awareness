using System.Threading.Tasks;

namespace VRD_AIProviders
{
    public interface ITTSService 
    {
        Task<TTSResponse> SynthesizeAsync(TTSRequest request);
    }

}
