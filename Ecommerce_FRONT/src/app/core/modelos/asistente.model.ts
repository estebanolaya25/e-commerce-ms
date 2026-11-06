export type RolMensaje = 'user' | 'assistant';

// Producto que el asistente recomienda. disponible es null si no se pudo confirmar con Inventario.
export interface ProductoSugerido {
  slug: string;
  nombre: string;
  marca: string | null;
  precio: number;
  precioAntes: number | null;
  imagenUrl: string | null;
  disponible: boolean | null;
}

// Acción que el asistente propone; solo se ejecuta si el cliente pulsa «Confirmar» (lo hace el frontend, no el asistente).
export interface AccionPropuesta {
  tipo: 'agregar_carrito' | 'cancelar_pedido' | 'avanzar_pedido';
  descripcion: string;
  sku: string | null;
  cantidad: number | null;
  slug: string | null;
  pedidoId: string | null;
  numero: string | null;
  // Solo en avanzar_pedido (panel interno): estado al que pasa el pedido.
  estadoNuevo: string | null;
}

export type EstadoAccion = 'pendiente' | 'ejecutando' | 'hecha' | 'descartada' | 'error';

export interface AccionEnChat extends AccionPropuesta {
  estado: EstadoAccion;
  mensaje?: string;
}

export interface MensajeAsistente {
  role: RolMensaje;
  content: string;
  // Documentos de la base de conocimiento usados para la respuesta (solo en mensajes del asistente).
  fuentes?: string[];
  productos?: ProductoSugerido[];
  acciones?: AccionEnChat[];
}

// Sugerencia de descripción para el formulario de producto (panel de catálogo).
export interface DatosDescripcion {
  nombre: string;
  categoria: string;
  marca: string | null;
  atributos: Record<string, string>;
  opciones: Record<string, string[]>;
  descripcionActual: string | null;
}

export interface DescripcionSugerida {
  descripcionCorta: string;
  descripcion: string;
}

export interface RespuestaAsistente {
  reply: string;
  sources: string[];
  productos: ProductoSugerido[];
  acciones: AccionPropuesta[];
}
