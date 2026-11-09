namespace Statevia.Infrastructure.Persistence;

/// <summary><see cref="ICoreUnitOfWork"/> から EF Core 実装を取得する。</summary>
internal static class CoreUnitOfWorkExtensions
{
    /// <summary>EF Core <see cref="CoreDbContext"/> を返す。</summary>
    /// <param name="uow">EF Core 実装のユニットオブワーク。</param>
    /// <returns><paramref name="uow"/> の DbContext。</returns>
    /// <exception cref="InvalidCastException"><paramref name="uow"/> の Db が <see cref="CoreDbContext"/> でないとき。</exception>
    internal static CoreDbContext GetDb(this ICoreUnitOfWork uow) =>
        (CoreDbContext)uow.Db;
}
