import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-paginador',
  standalone: false,
  templateUrl: './paginador.html',
  styleUrl: './paginador.scss'
})
export class Paginador {
  @Input() pagina = 1;
  @Input() totalPaginas = 1;
  @Output() cambiar = new EventEmitter<number>();

  // Muestra la primera, la última y las páginas cercanas a la actual; el resto se resume con "…".
  get paginas(): (number | null)[] {
    const visibles = new Set([1, this.totalPaginas, this.pagina - 1, this.pagina, this.pagina + 1]);
    const ordenadas = [...visibles].filter(p => p >= 1 && p <= this.totalPaginas).sort((a, b) => a - b);
    const resultado: (number | null)[] = [];
    ordenadas.forEach((p, i) => {
      if (i > 0 && p - ordenadas[i - 1] > 1) {
        resultado.push(null);
      }
      resultado.push(p);
    });
    return resultado;
  }

  ir(pagina: number): void {
    if (pagina >= 1 && pagina <= this.totalPaginas && pagina !== this.pagina) {
      this.cambiar.emit(pagina);
    }
  }
}
