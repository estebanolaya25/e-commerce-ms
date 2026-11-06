import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API } from '../config/api.config';
import { peticionSilenciosa } from '../interceptores/contexto-http';
import { DatosDescripcion, DescripcionSugerida, MensajeAsistente, RespuestaAsistente } from '../modelos/asistente.model';

@Injectable({ providedIn: 'root' })
export class AsistenteService {
  constructor(private http: HttpClient) {}

  // Se envía el historial para que entienda preguntas de seguimiento ("¿y si es a Cali?").
  // Sin barra de carga global: el chat muestra su propio indicador mientras responde.
  preguntar(historial: MensajeAsistente[]): Observable<RespuestaAsistente> {
    const messages = historial.map(({ role, content }) => ({ role, content }));
    return this.http.post<RespuestaAsistente>(`${API.asistente}/chat`, { messages }, { context: peticionSilenciosa() });
  }

  // Asistente del panel interno: sus herramientas dependen del rol del empleado (lo decide el backend).
  preguntarPanel(historial: MensajeAsistente[]): Observable<RespuestaAsistente> {
    const messages = historial.map(({ role, content }) => ({ role, content }));
    return this.http.post<RespuestaAsistente>(`${API.asistente}/panel/chat`, { messages }, { context: peticionSilenciosa() });
  }

  generarDescripcion(datos: DatosDescripcion): Observable<DescripcionSugerida> {
    return this.http.post<DescripcionSugerida>(`${API.asistente}/panel/descripcion`, datos, { context: peticionSilenciosa() });
  }
}
