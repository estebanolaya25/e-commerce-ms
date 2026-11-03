// Los servicios envían las fechas en UTC con "Z" final ("2026-10-31T18:15:31Z").
// Si alguna llegara sin zona horaria, se interpreta como UTC y no como hora local (5 horas de diferencia en Colombia).
export function fechaUtc(texto: string): Date {
  return new Date(/[zZ]|[+-]\d{2}:\d{2}$/.test(texto) ? texto : `${texto}Z`);
}
