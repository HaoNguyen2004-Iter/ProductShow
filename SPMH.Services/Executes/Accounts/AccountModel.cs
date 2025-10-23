namespace SPMH.Services.Executes.Accounts
{
    public class AccountModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public AccountModel() { }

        public AccountModel(
            int id,
            string username,
            string password)
        {
            Id = id;
            Username = username ?? string.Empty;
            Password = password ?? string.Empty;
        }
    }
}