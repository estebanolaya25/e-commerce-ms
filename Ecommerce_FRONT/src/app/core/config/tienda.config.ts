// Reglas de la tienda que el frontend muestra antes del checkout.
// El valor definitivo lo calcula Pedidos (Pedidos:EnvioGratisDesde y Pedidos:CostoEnvio).
export const TIENDA = {
  envioGratisDesde: 150000,
  costoEnvio: 12000,
  maximoPorProducto: 10
} as const;
