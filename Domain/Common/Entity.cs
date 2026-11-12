namespace Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    // Token de concurrencia optimista. Infrastructure lo mapea (p. ej. xmin en PostgreSQL)
    // para que dos asignaciones simultáneas sobre el mismo registro no se pisen.
    public uint Version { get; private set; }
}
