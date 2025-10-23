namespace SPMH.DBContext.Entities
{
    public class Account
    {
        public int Id { get; set; }
        public string Username { get; set; } = default!;

        public string Password { get; set; } = default!;

    }
}