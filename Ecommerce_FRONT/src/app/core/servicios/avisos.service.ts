import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type TipoAviso = 'exito' | 'error' | 'info';

export interface Aviso {
  id: number;
  tipo: TipoAviso;
  titulo: string;
  mensaje?: string;
  enlace?: { texto: string; ruta: string };
}

// Notificaciones flotantes (toasts) que se cierran solas.
@Injectable({ providedIn: 'root' })
export class AvisosService {
  private siguienteId = 1;
  private readonly avisosSubject = new BehaviorSubject<Aviso[]>([]);
  readonly avisos$ = this.avisosSubject.asObservable();

  exito(titulo: string, mensaje?: string, enlace?: Aviso['enlace']): void {
    this.mostrar({ tipo: 'exito', titulo, mensaje, enlace });
  }

  error(titulo: string, mensaje?: string): void {
    this.mostrar({ tipo: 'error', titulo, mensaje }, 6000);
  }

  info(titulo: string, mensaje?: string): void {
    this.mostrar({ tipo: 'info', titulo, mensaje });
  }

  cerrar(id: number): void {
    this.avisosSubject.next(this.avisosSubject.value.filter(aviso => aviso.id !== id));
  }

  private mostrar(aviso: Omit<Aviso, 'id'>, milisegundos = 4000): void {
    const id = this.siguienteId++;
    this.avisosSubject.next([...this.avisosSubject.value, { ...aviso, id }].slice(-3));
    setTimeout(() => this.cerrar(id), milisegundos);
  }
}
