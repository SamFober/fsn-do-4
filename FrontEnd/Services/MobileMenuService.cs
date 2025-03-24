namespace FrontEnd.Services
{

    public interface IMobileMenuService
    {
        Task CloseMobileMenu();
        void RegisterMenuCloseCallback(Action callback);
    }
    public class MobileMenuService : IMobileMenuService
    {
        private Action? OnMenuClosed;

        public void RegisterMenuCloseCallback(Action callback)
        {
            OnMenuClosed = callback;
        }

        public Task CloseMobileMenu()
        {
            OnMenuClosed?.Invoke();
            return Task.CompletedTask;
        }
    }
}
