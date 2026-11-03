export type EstadoItemCarrito =
  | 'Disponible'
  | 'PocasUnidades'
  | 'StockInsuficiente'
  | 'Agotado'
  | 'NoDisponible'
  | 'SinInformacion';

export interface ItemCarrito {
  sku: string;
  cantidad: number;
  nombre: string | null;
  slug: string | null;
  opciones: Record<string, string>;
  imagenUrl: string | null;
  precioUnitario: number | null;
  precioAntes: number | null;
  subtotal: number | null;
  stockDisponible: number | null;
  estado: EstadoItemCarrito;
}

// Respuesta de /api/carrito (microservicio Carrito).
export interface Carrito {
  items: ItemCarrito[];
  productosDistintos: number;
  unidades: number;
  total: number;
  puedeComprar: boolean;
  informacionCompleta: boolean;
  avisos: string[];
}
