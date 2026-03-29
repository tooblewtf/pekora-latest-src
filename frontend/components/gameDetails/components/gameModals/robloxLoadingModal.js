import React, { useEffect, useState } from "react";
import { createUseStyles } from "react-jss";
import ActionButton from "../../../actionButton";
import useButtonStyles from "../../../../styles/buttonStyles";
import NewModal from "../../../newModal";
import { multiGetPresence } from "../../../../services/presence";
import AuthenticationStore from "../../../../stores/authentication";
import DownloadRobloxModal from "./downloadRobloxModal";

const useStyles = createUseStyles({
    buttonRow: {
        display: 'flex',
        marginTop: '20px'
    },

    modal: {
        width: '475px',
        height: 'auto',
        aspectRatio: '612 / 385'
    },
    modalTopBody: {
        //color: 'var(--text-color-primary)',
    },
    modalMessage: {
        fontSize: '16px',
        fontWeight: '400',
        lineHeight: '1.4em',
        marginTop: '15px',
    },
    modalBtns: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        gap: '15px',
        margin: '6px 0 0',
        textAlign: 'center',
        '& *': {
            minWidth: '90px',
            margin: 0,
        }
    },
    button: {
        lineHeight: '2em',
        fontSize: '20px',
    },
    row: {
        margin: 0,
        display: 'flex',
        height: '100%',
    },


    container: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        paddingBottom: '10px'
    },
    loadingText: {
        lineHeight: '1.5em',
        whiteSpace: 'pre-line',
        wordWrap: 'break-word',
        hyphens: 'none',
        margin: 0,
        color: 'var(--text-color-primary)',
        textAlign: 'center',
    },
    iconLogo: {
        width: '95px',
        height: '95px',
        display: 'inline-block',
        verticalAlign: 'middle',
        backgroundImage: 'url(/img/favicon.png)',
        backgroundSize: '85px 85px',
        backgroundRepeat: 'no-repeat',
        backgroundPosition: '5px 0'
    },
})

/**
 * 
 * @param {{exitFunction: () => void; closeModals: boolean;}} props 
 * @returns 
 */
const projexLoadingModal = props => {
    const s = useStyles();
    const [isOpen, setOpen] = useState(true);

    useEffect(() => {
        setTimeout(() => {
            setOpen(false);
        }, 5000);
    }, [props])

    return <>
        {!isOpen && <DownloadRobloxModal exitFunction={props.exitFunction} closeModals={props.closeModals} />}
        {isOpen && <NewModal title="">
            <div className={s.container}>
                <span className={s.iconLogo} />
                <p className={s.loadingText}>Marine is now loading. Get ready!</p>
                <span className="spinner" />
            </div>
            <div className={s.modalBtns}></div>
        </NewModal>
        }
    </>
}

export default projexLoadingModal;