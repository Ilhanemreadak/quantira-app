using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Reflection.Emit;

namespace Quantira.Infrastructure.Persistence
{
    public class QuantiraDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public QuantiraDbContext(DbContextOptions<QuantiraDbContext> options)
            : base(options) { }

        public DbSet<Portfolio> Portfolios => Set<Portfolio>();
        public DbSet<Asset> Assets => Set<Asset>();
        public DbSet<Position> Positions => Set<Position>();
        public DbSet<Trade> Trades => Set<Trade>();
        public DbSet<Alert> Alerts => Set<Alert>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.ApplyConfigurationsFromAssembly(typeof(QuantiraDbContext).Assembly);
        }
    }

}
