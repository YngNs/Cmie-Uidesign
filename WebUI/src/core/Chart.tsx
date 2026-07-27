import { useEffect, useRef } from 'react'
import * as echarts from 'echarts'

/**
 * 轻量 ECharts 封装：option 变化时增量 setOption，容器尺寸自适应。
 * dark 仅用于决定默认文字颜色，具体配色由 option 指定。
 */
export function Chart({ option, className, style, onClickPoint }: {
  option: echarts.EChartsOption
  className?: string
  style?: React.CSSProperties
  onClickPoint?: (seriesIndex: number, dataIndex: number) => void
}) {
  const ref = useRef<HTMLDivElement>(null)
  const chartRef = useRef<echarts.ECharts | null>(null)

  useEffect(() => {
    if (!ref.current) return
    const chart = echarts.init(ref.current)
    chartRef.current = chart
    const ro = new ResizeObserver(() => chart.resize())
    ro.observe(ref.current)
    return () => { ro.disconnect(); chart.dispose(); chartRef.current = null }
  }, [])

  useEffect(() => {
    const chart = chartRef.current
    if (!chart) return
    chart.setOption(option, { notMerge: true })
    chart.off('click')
    if (onClickPoint) {
      chart.on('click', (p: any) => {
        if (p.componentType === 'series') onClickPoint(p.seriesIndex, p.dataIndex)
      })
    }
  }, [option, onClickPoint])

  return <div ref={ref} className={className} style={{ width: '100%', height: '100%', ...style }} />
}
