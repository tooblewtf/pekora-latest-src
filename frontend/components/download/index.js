import React from "react";
import { createUseStyles } from "react-jss";
import getFlag from "../../lib/getFlag";
import DownloadButton from "./components/downloadButton";

const useDownloadStyles = createUseStyles({
    title: {
        color: 'var(--text-color-primary)',
        fontSize: '32px',
        fontWeight: '700',
    },
    subTitle: {
        fontSize: '14px',
    },
})

const Download = props => {
    const s = useDownloadStyles();

    return <div className='container'>
        <div className='row'>
            <div className='col-12 mb-4'>
                <h1 className={s.title}>Download</h1>
                <p className={s.subTitle}>Download the Marine Player to get into the game.</p>
            </div>
        </div>
        <div className='row'>
            {
                getFlag('downloadGameClients', []).map(v => {
                    return <DownloadButton key={v.title + v.url + v.imageUrl} url={v.url} title={v.title} imageUrl={v.imageUrl}></DownloadButton>
                })
            }
        </div>
    </div>
}

export default Download;