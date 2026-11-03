export type EstadoPedido =
  | 'PendientePago'
  | 'PagoEnProceso'
  | 'Pagado'
  | 'EnPreparacion'
  | 'Enviado'
  | 'Entregado'
  | 'Cancelado'
  | 'Expirado';

export interface DireccionEnvio {
  nombreDestinatario: string;
  telefono: string;
  direccion: string;
  ciudad: string;
  departamento: string;
  notas?: string | null;
}

export interface ItemPedido {
  sku: string;
  nombre: string;
  opciones: string | null;
  imagenUrl: string | null;
  precioUnitario: number;
  cantidad: number;
  subtotal: number;
}

export interface HistorialPedido {
  estadoAnterior: EstadoPedido | null;
  estadoNuevo: EstadoPedido;
  motivo: string | null;
  createdAt: string;
}

// Respuesta de /api/pedidos/{id} (microservicio Pedidos).
export interface Pedido {
  pedidoId: string;
  numero: string;
  usuarioEmail: string | null;
  estado: EstadoPedido;
  subtotal: number;
  costoEnvio: number;
  total: number;
  moneda: string;
  direccion: DireccionEnvio;
  pagoId: string | null;
  ultimoErrorPago: string | null;
  expiraEn: string;
  createdAt: string;
  updatedAt: string;
  items: ItemPedido[];
  historial: HistorialPedido[];
}

// Cada fila de /api/pedidos (Mis pedidos).
export interface PedidoResumen {
  pedidoId: string;
  numero: string;
  estado: EstadoPedido;
  total: number;
  unidades: number;
  expiraEn: string;
  createdAt: string;
}

export interface Tarjeta {
  numero: string;
  titular: string;
  mesVencimiento: number;
  anioVencimiento: number;
  cvv: string;
}

export interface PagoIniciado {
  pedidoId: string;
  pagoId: string;
  estadoPedido: EstadoPedido;
  mensaje: string;
}

// Cómo se muestra cada estado en la interfaz.
export const ESTADOS_PEDIDO: Record<EstadoPedido, { texto: string; clase: string; icono: string }> = {
  PendientePago: { texto: 'Pendiente de pago', clase: 'advertencia', icono: 'bi-hourglass-split' },
  PagoEnProceso: { texto: 'Pago en proceso', clase: 'info', icono: 'bi-arrow-repeat' },
  Pagado: { texto: 'Pagado', clase: 'primario', icono: 'bi-credit-card-2-front' },
  EnPreparacion: { texto: 'En preparación', clase: 'primario', icono: 'bi-box-seam' },
  Enviado: { texto: 'Enviado', clase: 'info', icono: 'bi-truck' },
  Entregado: { texto: 'Entregado', clase: 'exito', icono: 'bi-house-check' },
  Cancelado: { texto: 'Cancelado', clase: 'neutro', icono: 'bi-x-circle' },
  Expirado: { texto: 'Expirado', clase: 'neutro', icono: 'bi-clock-history' }
};

// Pedidos ya pagados: los únicos que bodega puede ver y despachar.
export const ESTADOS_DESPACHABLES: EstadoPedido[] = ['Pagado', 'EnPreparacion', 'Enviado', 'Entregado'];
