using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Data;

public sealed class HookHarborDbContext(DbContextOptions<HookHarborDbContext> options) : DbContext(options)
{

}