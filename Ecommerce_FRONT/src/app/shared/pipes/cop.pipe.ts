import { Pipe, PipeTransform } from '@angular/core';

// Formatea pesos colombianos sin decimales: 129900 -> "$ 129.900".
@Pipe({ name: 'cop', standalone: false })
export class CopPipe implements PipeTransform {
  private static readonly formato = new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    maximumFractionDigits: 0
  });

  transform(valor: number | null | undefined): string {
    return valor == null ? '' : CopPipe.formato.format(valor);
  }
}
