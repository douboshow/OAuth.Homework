using Microsoft.EntityFrameworkCore;

namespace OAuth.Homework.Models
{
    public class SubscriberContext : DbContext
    {
        public SubscriberContext(DbContextOptions options): base(options) {}

        public DbSet<Subscriber> Subscribers { get; set; }
    }
}
