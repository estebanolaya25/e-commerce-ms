import { HttpClient, HttpDownloadProgressEvent, HttpEventType } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API } from '../config/api.config';
import { peticionSilenciosa } from '../interceptores/contexto-http';
import {
  DatosDescripcion, DescripcionSugerida, EventoAsistente, MensajeAsistente
} from '../modelos/asistente.model';

@Injectable({ providedIn: 'root' })
export class AsistenteService {
  constructor(private http: HttpClient) {}

  // Se envía el historial para que entienda preguntas de seguimiento ("¿y si es a Cali?"). En modo panel las
  // herramientas dependen del rol del empleado (lo decide el backend). Sin barra de carga global: el chat muestra
  // su propio indicador.
  // La respuesta llega por partes (Server-Sent Events) mientras el modelo la genera. Se usa HttpClient, no fetch ni
  // EventSource, para pasar por los interceptores (token y renovación de la sesión): el texto parcial llega en los
  // eventos de progreso de descarga. Los endpoints sin streaming (/chat, /panel/chat) siguen en la API para otros
  // clientes, como la app móvil.
  preguntarEnVivo(historial: MensajeAsistente[], modo: 'tienda' | 'panel'): Observable<EventoAsistente> {
    const url = modo === 'panel' ? `${API.asistente}/panel/chat/stream` : `${API.asistente}/chat/stream`;
    return new Observable<EventoAsistente>(suscriptor => {
      let leidos = 0;
      // Cada evento termina en una línea en blanco; solo se procesan los bloques completos.
      const procesar = (texto: string) => {
        let fin: number;
        while ((fin = texto.indexOf('\n\n', leidos)) !== -1) {
          const bloque = texto.slice(leidos, fin);
          leidos = fin + 2;
          for (const linea of bloque.split('\n')) {
            if (linea.startsWith('data: ')) {
              try {
                suscriptor.next(JSON.parse(linea.slice(6)) as EventoAsistente);
              } catch {
                // Un bloque mal formado no debe cortar la conversación.
              }
            }
          }
        }
      };

      const peticion = this.http
        .post(url, { messages: this.mensajes(historial) }, {
          observe: 'events',
          reportProgress: true,
          responseType: 'text',
          context: peticionSilenciosa()
        })
        .subscribe({
          next: evento => {
            if (evento.type === HttpEventType.DownloadProgress) {
              procesar((evento as HttpDownloadProgressEvent).partialText ?? '');
            } else if (evento.type === HttpEventType.Response) {
              procesar(evento.body ?? '');
              suscriptor.complete();
            }
          },
          error: error => suscriptor.error(error)
        });
      return () => peticion.unsubscribe();
    });
  }

  generarDescripcion(datos: DatosDescripcion): Observable<DescripcionSugerida> {
    return this.http.post<DescripcionSugerida>(`${API.asistente}/panel/descripcion`, datos, { context: peticionSilenciosa() });
  }

  private mensajes(historial: MensajeAsistente[]) {
    return historial.filter(m => !m.enCurso).map(({ role, content }) => ({ role, content }));
  }
}
