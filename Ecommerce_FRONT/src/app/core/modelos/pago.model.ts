export type EstadoPago = 'Procesando' | 'Aprobado' | 'Rechazado' | 'Reembolsado';

// Respuesta de /api/pagos (microservicio Pagos). Cada intento de pago de un pedido es un registro.
export interface Pago {
  pagoId: string;
  pedidoId: string;
  monto: number;
  moneda: string;
  estado: EstadoPago;
  marca: string;
  ultimos4: string;
  titular: string;
  referencia: string | null;
  motivoRechazo: string | null;
  createdAt: string;
  procesadoEn: string | null;
  reembolsadoEn: string | null;
}

export const ESTADOS_PAGO: Record<EstadoPago, { texto: string; icono: string; clase: string }> = {
  Procesando: { texto: 'Procesando', icono: 'bi-arrow-repeat', clase: 'info' },
  Aprobado: { texto: 'Aprobado', icono: 'bi-check-circle', clase: 'exito' },
  Rechazado: { texto: 'Rechazado', icono: 'bi-x-circle', clase: 'peligro' },
  Reembolsado: { texto: 'Reembolsado', icono: 'bi-arrow-counterclockwise', clase: 'neutro' }
};
