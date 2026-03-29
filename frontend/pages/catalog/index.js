import React from "react";
import { createUseStyles } from "react-jss";
import AdBanner from "../../components/ad/adBanner";
import CatalogLegend from "../../components/catalogLegend";
import CatalogPageInput from "../../components/catalogPageInput";
import CatalogPageNavigation from "../../components/catalogPageNavigation";
import CatalogPageResults from "../../components/catalogPageResults";
import CatalogPageStore from "../../stores/catalogPage";
import CatalogFilters from "../../components/catalogFilters";

const useStyles = createUseStyles({
  title: {
    fontWeight: 700,
    fontSize: '32px',
    marginBottom: '12px',
    color: 'var(--text-color-primary)',
  },
  catalogContainer: {
    backgroundColor: 'var(--white-color)',
    padding: '2px 4px',
  },
})

const CatalogPage = props => {
  const s = useStyles();
  return <CatalogPageStore.Provider>
      <div className='container mt-4 ssp'>
        <AdBanner/>
        <div className={s.catalogContainer}>
          <div className='row mt-2'>
            <div className='col-12 col-md-4 col-lg-2'>
              <h1 className={s.title}>Catalog</h1>
            </div>
            <div className='col-12 col-md-8 col-lg-10'>
              <CatalogPageInput/>
            </div>
          </div>
          <div className='row'>
            <div className='col-12 col-md-4 col-lg-2'>
              <div className='divider-right'>
                <div className='pe-2'>
                  <CatalogPageNavigation/>
                  <CatalogFilters/>
                  <CatalogLegend/>
                </div>
              </div>
            </div>
            <div className='col-12 col-md-8 col-lg-10'>
              <CatalogPageResults/>
            </div>
          </div>
        </div>
      </div>
    </CatalogPageStore.Provider>
}

CatalogPage.getInitialProps = () => {
  return {
    title: 'Catalog - Marine',
  }
}

export default CatalogPage;
