import { Component, ElementRef, HostListener, OnDestroy, ViewChild } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { MensajeAsistente } from '../../../core/modelos/asistente.model';
import { AsistenteService } from '../../../core/servicios/asistente.service';
import { mensajeDeError } from '../../../core/utilidades/errores-http';

const CLAVE_HISTORIAL = 'tienda.asistente.historial';
const MAX_CARACTERES = 1000;

// Chat flotante con el asistente virtual. Vive en la raíz de la app: la conversación sigue al cambiar de página
// y se guarda en sessionStorage para que sobreviva a recargar la pestaña (no a cerrarla).
@Component({
  selector: 'app-asistente-chat',
  standalone: false,
  templateUrl: './asistente-chat.html',
  styleUrl: './asistente-chat.scss'
})
export class AsistenteChat implements OnDestroy {
  @ViewChild('entrada') private entrada?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('lista') private lista?: ElementRef<HTMLDivElement>;

  readonly maxCaracteres = MAX_CARACTERES;
  readonly sugerencias = [
    'Busco una chaqueta para la lluvia',
    '¿Cuánto cuesta el envío?',
    '¿Cómo cancelo un pedido?',
    '¿Cómo sé qué talla soy?'
  ];

  abierto = false;
  visible = true;
  respondiendo = false;
  error: string | null = null;
  texto = '';
  mensajes: MensajeAsistente[] = this.leerHistorial();

  private readonly navegacion: Subscription;

  constructor(private asistente: AsistenteService, router: Router) {
    // El panel interno tiene su propio flujo de trabajo; en esta fase el asistente es solo para clientes.
    const actualizarVisibilidad = (url: string) => (this.visible = !url.startsWith('/admin'));
    actualizarVisibilidad(router.url);
    this.navegacion = router.events
      .pipe(filter(evento => evento instanceof NavigationEnd))
      .subscribe(evento => actualizarVisibilidad((evento as NavigationEnd).urlAfterRedirects));
  }

  ngOnDestroy(): void {
    this.navegacion.unsubscribe();
  }

  alternar(): void {
    this.abierto = !this.abierto;
    if (this.abierto) {
      this.enfocarEntrada();
      this.bajarAlFinal();
    }
  }

  @HostListener('document:keydown.escape')
  cerrar(): void {
    this.abierto = false;
  }

  alEscribir(evento: KeyboardEvent): void {
    // Enter envía; Shift+Enter hace un salto de línea.
    if (evento.key === 'Enter' && !evento.shiftKey) {
      evento.preventDefault();
      this.enviar();
    }
  }

  enviar(pregunta = this.texto): void {
    const contenido = pregunta.trim().slice(0, MAX_CARACTERES);
    if (!contenido || this.respondiendo) {
      return;
    }
    this.texto = '';
    this.mensajes = [...this.mensajes, { role: 'user', content: contenido }];
    this.solicitarRespuesta();
  }

  // Tras un error, la última pregunta sigue en la conversación: se vuelve a pedir la respuesta.
  reintentar(): void {
    if (!this.respondiendo && this.mensajes.at(-1)?.role === 'user') {
      this.solicitarRespuesta();
    }
  }

  // En celular el chat ocupa toda la pantalla: al abrir un producto se cierra para que se vea la página.
  alAbrirProducto(): void {
    if (window.matchMedia('(max-width: 575.98px)').matches) {
      this.cerrar();
    }
  }

  nuevaConversacion(): void {
    if (this.respondiendo) {
      return;
    }
    this.mensajes = [];
    this.error = null;
    this.guardarHistorial();
    this.enfocarEntrada();
  }

  // Convierte el markdown sencillo que usa el modelo (negritas, viñetas, saltos de línea) en HTML.
  // Se escapa todo primero: el texto del modelo nunca se inserta como HTML sin escapar.
  formatear(texto: string): string {
    const escapado = texto
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');

    const html: string[] = [];
    let enLista = false;
    for (const linea of escapado.split('\n')) {
      const vineta = /^\s*(?:[-*•]|\d+[.)])\s+(.*)$/.exec(linea);
      if (vineta) {
        if (!enLista) {
          html.push('<ul>');
          enLista = true;
        }
        html.push(`<li>${this.negritas(vineta[1])}</li>`);
        continue;
      }
      if (enLista) {
        html.push('</ul>');
        enLista = false;
      }
      html.push(linea.trim() ? `<p>${this.negritas(linea)}</p>` : '');
    }
    if (enLista) {
      html.push('</ul>');
    }
    return html.join('');
  }

  private negritas(linea: string): string {
    return linea.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
  }

  private solicitarRespuesta(): void {
    this.error = null;
    this.respondiendo = true;
    this.guardarHistorial();
    this.bajarAlFinal();

    this.asistente.preguntar(this.mensajes).subscribe({
      next: respuesta => {
        this.mensajes = [...this.mensajes, {
          role: 'assistant',
          content: respuesta.reply,
          // Si recomendó productos, las fuentes de la base de conocimiento no aportan: se muestran las tarjetas.
          fuentes: respuesta.productos?.length ? [] : respuesta.sources,
          productos: respuesta.productos ?? []
        }];
        this.respondiendo = false;
        this.guardarHistorial();
        this.bajarAlFinal();
        this.enfocarEntrada();
      },
      error: error => {
        this.error = mensajeDeError(error, 'No pude responder en este momento. Intente de nuevo.');
        this.respondiendo = false;
        this.bajarAlFinal();
      }
    });
  }

  private enfocarEntrada(): void {
    setTimeout(() => this.entrada?.nativeElement.focus());
  }

  private bajarAlFinal(): void {
    setTimeout(() => {
      const lista = this.lista?.nativeElement;
      if (lista) {
        lista.scrollTop = lista.scrollHeight;
      }
    });
  }

  private leerHistorial(): MensajeAsistente[] {
    try {
      const guardado = JSON.parse(sessionStorage.getItem(CLAVE_HISTORIAL) ?? '[]');
      return Array.isArray(guardado) ? guardado : [];
    } catch {
      return [];
    }
  }

  private guardarHistorial(): void {
    try {
      sessionStorage.setItem(CLAVE_HISTORIAL, JSON.stringify(this.mensajes));
    } catch {
      // Sin almacenamiento (modo privado o bloqueado): la conversación solo dura mientras la página esté abierta.
    }
  }
}
