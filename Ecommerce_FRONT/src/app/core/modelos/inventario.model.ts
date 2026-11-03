// Respuesta de /api/inventario/disponibilidad (microservicio Inventario).
export interface Disponibilidad {
  sku: string;
  existe: boolean;
  disponible: number;
  agotado: boolean;
  pocasUnidades: boolean;
}

// ---- Panel de administración (Administrator, Warehouse) ----

// Disponible = físico - reservado (lo apartado por pedidos que aún no se pagan).
export interface StockSku {
  sku: string;
  productoId: string | null;
  varianteId: string | null;
  cantidadFisica: number;
  cantidadReservada: number;
  cantidadDisponible: number;
  stockMinimo: number;
  bajoMinimo: boolean;
  updatedAt: string;
}

export type TipoMovimiento = 'Alta' | 'Entrada' | 'Ajuste' | 'Reserva' | 'Venta' | 'Liberacion' | 'Expiracion';

export interface MovimientoStock {
  movimientoId: number;
  sku: string;
  tipo: TipoMovimiento;
  cantidad: number;
  fisicaResultante: number;
  reservadaResultante: number;
  referenciaId: string | null;
  motivo: string | null;
  usuarioId: string | null;
  createdAt: string;
}

export const TIPOS_MOVIMIENTO: Record<TipoMovimiento, { texto: string; icono: string; clase: string; ayuda: string }> = {
  Alta: { texto: 'Alta', icono: 'bi-plus-circle', clase: 'neutro', ayuda: 'El SKU se registró en inventario' },
  Entrada: { texto: 'Entrada', icono: 'bi-box-arrow-in-down', clase: 'exito', ayuda: 'Llegó mercancía (suma al físico)' },
  Ajuste: { texto: 'Ajuste', icono: 'bi-sliders', clase: 'info', ayuda: 'Corrección manual del físico' },
  Reserva: { texto: 'Reserva', icono: 'bi-lock', clase: 'advertencia', ayuda: 'Apartado para un pedido pendiente de pago' },
  Venta: { texto: 'Venta', icono: 'bi-bag-check', clase: 'primario', ayuda: 'Pedido pagado: sale del físico y de lo reservado' },
  Liberacion: { texto: 'Liberación', icono: 'bi-unlock', clase: 'neutro', ayuda: 'Pedido cancelado: se suelta la reserva' },
  Expiracion: { texto: 'Expiración', icono: 'bi-hourglass-bottom', clase: 'neutro', ayuda: 'Pedido sin pagar a tiempo: se suelta la reserva' }
};

export type VistaStock = 'todos' | 'bajo-minimo' | 'agotados';
