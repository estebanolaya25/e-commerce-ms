export type RolMensaje = 'user' | 'assistant';

export interface MensajeAsistente {
  role: RolMensaje;
  content: string;
  // Documentos de la base de conocimiento usados para la respuesta (solo en mensajes del asistente).
  fuentes?: string[];
}

export interface RespuestaAsistente {
  reply: string;
  sources: string[];
}
