using System.Threading.Tasks;

namespace VRD_AIProviders
{
    public interface ILLMService
    {
        Task<ChatResponse> CompleteAsync(ChatRequest request);
    }

}

