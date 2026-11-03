import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, distinctUntilChanged, map, of, switchMap, timer } from 'rxjs';

// Cuenta las peticiones en curso. La barra solo aparece si algo tarda más de 250 ms: así no parpadea en las rápidas.
@Injectable({ providedIn: 'root' })
export class CargaService {
  private readonly enCurso = new BehaviorSubject(0);

  readonly activa$: Observable<boolean> = this.enCurso.pipe(
    map(cantidad => cantidad > 0),
    distinctUntilChanged(),
    switchMap(activa => (activa ? timer(250).pipe(map(() => true)) : of(false)))
  );

  iniciar(): void {
    this.enCurso.next(this.enCurso.value + 1);
  }

  terminar(): void {
    this.enCurso.next(Math.max(0, this.enCurso.value - 1));
  }
}
