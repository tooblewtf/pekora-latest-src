import {createUseStyles} from "react-jss";
import configureItemStore from "../stores/configureItemStore";
import Robux from "../../robux";
import {useState} from "react";
import Tickets from "../../tickets";
import getFlag from "../../../lib/getFlag";

const useStyles = createUseStyles({
  inline: {
    display: 'inline-block',
  },
  columnOne: {
    width: '150px',
    textAlign: 'right',
    paddingRight: '5px',
  }
});

const SellItemCurrency = props => {
  const {currency} = props; // 1 = Robux, 2 = tix
  const [feedback, setFeedback] = useState(null);
  const store = configureItemStore.useContainer();
  const s = useStyles();

  const [price, setPrice] = currency === 2 ? [store.priceTickets, store.setPriceTickets] : [store.price, store.setPrice];

  let marketplaceFee = Math.trunc(price * 0.3);
  if (marketplaceFee < 1) {
    marketplaceFee = 1;
  }
  let userEarns = price - marketplaceFee;
  if (userEarns < 0) {
    userEarns = 0;
  }

  const CurrencyWrapper = (props) => {
    const {children} = props;
    if (currency === 2) {
      return <Tickets>{children}</Tickets>
    }
    return <Robux>{children}</Robux>
  }

  return <div className='col-12 col-lg-6'>
  {feedback ? <p className='text-danger'>{feedback}</p> : null}
  <div className='mt-4'>
    <div className={s.inline + ' ' + s.columnOne}>
      <p className='fw-bolder'>Price:</p>
    </div>
    <div className={s.inline + ' pe-1'}>
      <CurrencyWrapper />
    </div>
    <div className={s.inline}>
      <input disabled={!store.isForSale || store.locked} type='text' value={store.isForSale ? price : ''} onChange={e => {
        let newValue = parseInt(e.currentTarget.value, 10);
        const isFree = (newValue === 0 && currency === 1);
        if ((newValue > 1000000 || newValue < 2 || isNaN(newValue)) && !isFree) {
          if (e.currentTarget.value !== '')
            setFeedback(
              currency === 1 ? 'Price must be between R$2 and R$1,000,000' : 'Price must be between T$2 and T$1,000,000'
            );
          setPrice(e.currentTarget.value);
        }else {
          setFeedback(null);
          setPrice(newValue);
        }
      }} />
    </div>
  </div>
  <div className='mt-2'>
    <div className={s.inline + ' ' + s.columnOne}>
      <p className='fw-bolder mb-0'>Marketplace Fee:</p>
    </div>
    <div className={s.inline + ' pe-1'}>
      <CurrencyWrapper>{store.isForSale ? marketplaceFee : '-'}</CurrencyWrapper>
    </div>
  </div>
  <div className='mt-0'>
    <div className={s.inline + ' ' + s.columnOne}>
      <p>(30% - Minimum 1)</p>
    </div>
  </div>
  <div className='mt-2'>
    <div className={s.inline + ' ' + s.columnOne}>
      <p>You Earn</p>
    </div>
    <div className={s.inline + ' pe-1'}>
      <CurrencyWrapper>{store.isForSale ? userEarns : '-'}</CurrencyWrapper>
    </div>
  </div>
  </div>
}

const SellItem = props => {
  const store = configureItemStore.useContainer();

  return <div className='row'>
    <div className='col-12'>
      <h3>Sell this Item</h3>
      <hr className='mt-0 mb-2' />
      <p className='ps-2 pe-2'>Check the box below and enter a price if you want to sell this item in the Marine catalog. Uncheck the box to remove the item from catalog.</p>
    </div>
    <div>
      <input type='checkbox' disabled={store.locked} checked={store.isForSale} onChange={e => {
        store.setIsForSale(e.currentTarget.checked);
      }} />
      <p className='d-inline ms-2'>Sell this item</p>
    </div>
    <SellItemCurrency currency={1} />
    {getFlag('sellItemForTickets', false) ? <SellItemCurrency currency={2} /> : null}
  </div>
}

export default SellItem;