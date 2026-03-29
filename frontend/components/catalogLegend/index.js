import { createUseStyles } from "react-jss";

const useCatalogLegendStyles = createUseStyles({
  summary: {
    paddingTop: '12px',
  },
  wrapper: {
    userSelect: 'none',
  }
});

const useLegendStyles = createUseStyles({
  title: {
    fontWeight: 600,
    marginBottom: 0,
  },
  desc: {
    marginBottom: '0',
    marginTop: 0,
    lineHeight: 'normal',
  },
})

const LegendEntry = props => {
  const s = useLegendStyles();
  return <div>
    <img src={props.image}></img>
    <p className={s.title}>{props.title}</p>
    <p className={s.desc}>{props.description}</p>
  </div>
}

const CatalogLegend = props => {
  const s = useCatalogLegendStyles();
  return <div className={s.wrapper}>
    <details open={true}>
      <summary className={s.summary}>Legend</summary>
      {
        [
          {
            image: '/img/overlay_bcCatalog.png',
            title: 'Builders Club Only',
            description: 'Only purchasable by Builders Club members.',
          },
          {
            image: '/img/limitedOverlay_small.png',
            title: 'Limited Items',
            description: 'Owners of these discontinued items can re-sell them to other users at any price.',
          },
          {
            image: '/img/limitedUOverlay_small.png',
            title: 'Limited Unique Items',
            description: 'A limited supply originally sold by Marine. Each unit is labeled with a serial number. Once sold out, owners can re-sell them to other users.',
          },
        ].map(v => {
          return <LegendEntry image={v.image} key={v.title} title={v.title} description={v.description}></LegendEntry>
        })
      }
    </details>
  </div>
}

export default CatalogLegend;