using Domain.Entities;

namespace Domain.Services;

public readonly record struct PropuestaInsercion(Ruta Ruta, int Posicion, double CostoAdicionalKm);
