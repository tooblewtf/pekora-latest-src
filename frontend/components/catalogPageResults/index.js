import React from "react";
import {createUseStyles} from "react-jss";
import CatalogPageStore from "../../stores/catalogPage";
import CatalogPageCard from "../catalogPageCard";
import CatalogPagination from "../catalogPagination";
import OldSelect from "../oldSelect";

const useResultStyles = createUseStyles({
    pageTitle: {
        fontSize: '28px',
        fontWeight: 500,
    },
    pageTitleAlt: {
        fontSize: '14px',
        fontWeight: 700,
        opacity: 1,
        letterSpacing: -0.2,
    },
    subtitleText: {
        fontSize: '12px',
        fontWeight: 500,
    },
    selectWrapper: {
        width: '200px',
        display: 'inline-block',
    },
    sortWrapper: {
        float: 'right',
    },
    sortByLabel: {
        paddingRight: '4px',
        fontWeight: 600,
        color: 'var(--text-color-primary)',
    },
    sortByLabelWrapper: {
        display: 'inline-block',
    },
});

const ResultsContainer = props => {
    const store = CatalogPageStore.useContainer();
    if (!props.showTopFour) {
        return <>
            {
                store.results.data.map((v, i) => {
                    return <CatalogPageCard key={v.id} {...v}></CatalogPageCard>
                })
            }
        </>
    }
    return <>
        {
            store.results.data.slice(0, 4).map((v, i) => {
                return <CatalogPageCard key={v.id} {...v} mode='large'></CatalogPageCard>
            })
        }
        {
            store.results.data.slice(4).map((v, i) => {
                return <CatalogPageCard key={v.id} {...v}></CatalogPageCard>
            })
        }
    </>
}

const getDisplayNameForCombination = (cat, subCat) => {
    let suffix = '';
    let prefix = '';
    if (subCat) {
        switch (subCat.toLowerCase()) {
            case 'all':
                if (!cat) {
                    return 'All';
                }
                suffix = 'all';
                break;
            case 'clothing':
                return 'All Clothing';
            case 'teeshirt':
            case 'teeshirts':
            case 'tshirt':
                suffix = 'T-Shirts';
                break;
            case 'pants':
                suffix = 'Pants';
                break;
            case 'shirts':
            case 'shirt':
                suffix = 'Shirts';
                break;
            case 'hats':
                suffix = 'Hats';
                break;
            case 'accessories':
                suffix = 'Hats';
                break;
            case 'packages':
                suffix = 'Packages';
                break;
            case 'face':
            case 'faces':
                suffix = 'Faces';
                break;
            case 'gear':
            case 'gears':
                suffix = 'Gears';
                break;
        }
    }
    if (cat) {
        switch (cat.toLowerCase()) {
            case 'collectibles':
                if (!subCat) {
                    return 'Collectibles';
                }
                prefix = 'Collectible';
                break;
        }
    }
    return prefix + ' ' + suffix;
}

const CatalogPageResults = props => {
    const s = useResultStyles();
    const store = CatalogPageStore.useContainer();
    let showTopFour = store.category === 'Featured';
    
    const getTitle = () => {
        if (store.category === 'Featured') {
            return <h1 className={s.pageTitle}>Featured Items on Marine</h1>;
        }
        let title = getDisplayNameForCombination(store.category, store.subCategory);
        const currentOffset = Math.trunc(store.page / store.limit + 1) || 0;
        const limit = store.limit < store.results.data.length ? store.limit : store.results.data.length;
        
        const moreAvailable = store.nextCursor !== null;
        
        return <>
            <h1 className={s.pageTitleAlt}>{title.toUpperCase()}</h1>
            <p className={s.subtitleText}>Showing {store.results.data.length === 0 ? '0' : currentOffset.toLocaleString()} {moreAvailable ? '- ' + limit.toLocaleString() : ''} of {(typeof store.total === 'number' ? store.total : 'many').toLocaleString()} results</p>
        </>
    }
    
    return <div className='row' style={store.locked ? {opacity: '0.25'} : undefined}>
        <div className='col-6'>
            {getTitle()}
        </div>
        <div className='col-6'>
            <div className={s.sortWrapper}>
                <div className={s.sortByLabelWrapper}>
                    <span className={s.sortByLabel}>Sort By: </span>
                </div>
                <div className={s.selectWrapper}>
                    <OldSelect onChange={(newSort) => {
                        store.setSort(parseInt(newSort, 10));
                    }} options={[
                        {
                            key: '0',
                            value: 'Relevance',
                        },
                        {
                            key: '3',
                            value: 'Recently updated',
                        },
                        {
                            key: '4',
                            value: 'Price (Low to High)',
                        },
                        {
                            key: '5',
                            value: 'Price (High to Low)',
                        },
                        {
                            key: '6',
                            value: 'RAP (Low to High)',
                        },
                        {
                            key: '7',
                            value: 'RAP (High to Low)',
                        },
                    ]} disabled={store.locked}></OldSelect>
                </div>
            </div>
        </div>
        <div className='col-12'>
            <div className='row'>
                {
                    store.results ? <ResultsContainer showTopFour={showTopFour}></ResultsContainer> : null
                }
            </div>
            <CatalogPagination></CatalogPagination>
        </div>
    </div>
}

export default CatalogPageResults;
