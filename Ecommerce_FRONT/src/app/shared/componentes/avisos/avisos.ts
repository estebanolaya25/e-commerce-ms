import { Component } from '@angular/core';
import { AvisosService } from '../../../core/servicios/avisos.service';

@Component({
  selector: 'app-avisos',
  standalone: false,
  templateUrl: './avisos.html',
  styleUrl: './avisos.scss'
})
export class Avisos {
  readonly avisos$;

  readonly iconos = { exito: 'bi-check-circle-fill', error: 'bi-exclamation-octagon-fill', info: 'bi-info-circle-fill' };

  constructor(private avisosService: AvisosService) {
    this.avisos$ = avisosService.avisos$;
  }

  cerrar(id: number): void {
    this.avisosService.cerrar(id);
  }
}
