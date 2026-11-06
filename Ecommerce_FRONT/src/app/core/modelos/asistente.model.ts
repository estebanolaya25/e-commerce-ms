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

export interface MensajeAsistente {
  role: RolMensaje;
  content: string;
  // Documentos de la base de conocimiento usados para la respuesta (solo en mensajes del asistente).
  fuentes?: string[];
  productos?: ProductoSugerido[];
}

export interface RespuestaAsistente {
  reply: string;
  sources: string[];
  productos: ProductoSugerido[];
}
