const nf = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 })

/** 67500 → «67 500 ₸». */
export const tenge = (n: number) => `${nf.format(n)} ₸`

/** Сан (саны, бонус): 1.5 → «1,5». */
export const num = (n: number) => nf.format(n)

/** Ақша есебі: екі таңбаға дөңгелектеу (0.1 + 0.2 қатесін болдырмау). */
export const round2 = (n: number) => Math.round(n * 100) / 100
