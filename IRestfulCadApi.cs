namespace CNSDemo
{
    public interface IRestfulCadApi
    {
        Task ClearSession(string token);
            
        Task<(bool, string, long)> StartSession();
    }

}