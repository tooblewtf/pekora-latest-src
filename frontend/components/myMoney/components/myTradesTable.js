import React from "react";
import { createUseStyles } from "react-jss";
import AuthenticationStore from "../../../stores/authentication";
import AdSkyscraper from "../../ad/adSkyscraper";
import MoneyPageStore from "../stores/moneyPageStore";
import TradeStore from "../stores/tradeStore";
import TradeEntry from "./tradeEntry";
import TradeModal from "./tradeModal";

const useStyles = createUseStyles({
  tradeTypeActions: {
    display: 'inline-block',
  },
  table: {
    width: '100%',
  },
  tableHead: {
    borderTop: '1px solid #b9b9b9',
    borderBottom: '2px solid var(--background-color)',
    background: '#f1f1f1',
  },
  tableHeadLabel: {
    paddingTop: '5px',
    paddingBottom: '5px',
    paddingLeft: '5px',
    color: '#5d5a5b',
  },
  tableHeadBorder: {
    borderRight: '2px solid #e1e1e1',
  },
  tableAction: {
    width: '60px',
  },
  feedbackWrapper: {
    border: '2px solid #839ec3',
    background: '#e6eefa',
    padding: '8px',
    marginBottom: '10px',
  },
});

const MyTradesTable = props => {
  const auth = AuthenticationStore.useContainer();
  const trades = TradeStore.useContainer();
  const s = useStyles();

  return <div className='row mt-2'>
    {trades.selectedTrade && <TradeModal></TradeModal>}
    <div className='col-12 col-lg-10'>
      {trades.feedback && <div className='row'>
        <div className='col-12'>
          <div className={s.feedbackWrapper}>
            <p className='mb-0'>{trades.feedback}</p>
          </div>
        </div>
      </div>}
      <div className='row'>
        <div className='col-12'>
          <p className={s.tradeTypeActions + ' fw-700 lighten-3'}>Trade Type: </p>
          <select className={s.tradeTypeActions + ' ms-2'} value={trades.tradeType} onChange={(e) => {
            trades.setTradeType(e.currentTarget.value);
          }}>
            <option value='inbound'>Inbound ({auth.notificationCount.trades})</option>
            <option value='outbound'>Outbound</option>
            <option value='completed'>Completed</option>
            <option value='inactive'>Inactive</option>
          </select>
          <p className={s.tradeTypeActions + ' ms-2'}>
            <a href='https://silrev.biz/help'>
              How do I send a trade?
            </a>
          </p>
        </div>
      </div>
      <div className='row'>
        <div className='col-12'>
          <table className={s.table}>
            <thead className={s.tableHead}>
              <tr>
                <th className={s.tableHeadLabel + ' ' + s.tableHeadBorder}>Date</th>
                <th className={s.tableHeadLabel + ' ' + s.tableHeadBorder}>Expires</th>
                <th className={s.tableHeadLabel + ' ' + s.tableHeadBorder}>Trade Partner</th>
                <th className={s.tableHeadLabel + ' ' + s.tableHeadBorder}>Status</th>
                <th className={s.tableHeadLabel + ' ' + s.tableAction}>Action</th>
              </tr>
            </thead>
            <tbody>
              {
                trades.trades && trades.trades.data.map(v => {
                  return <TradeEntry key={v.id} {...v}></TradeEntry>
                })
              }
            </tbody>
          </table>
          {
            trades.trades && trades.trades.data.length === 0 ? <p className='mt-4 text-center'>No trades available</p> : null
          }
        </div>
      </div>
    </div>
    <div className='col-2'>
      <AdSkyscraper context='MyTrades'></AdSkyscraper>
    </div>
  </div>
}

export default MyTradesTable;