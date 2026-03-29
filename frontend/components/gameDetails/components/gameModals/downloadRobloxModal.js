import React, { useEffect, useState } from "react";
import { createUseStyles } from "react-jss";
import ActionButton from "../../../actionButton";
import useButtonStyles from "../../../../styles/buttonStyles";
import NewModal from "../../../newModal";
import Link from "next/link";
import InstructionsModal from "./instructionsModal";
import { multiGetPresence } from "../../../../services/presence";
import AuthenticationStore from "../../../../stores/authentication";

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
        justifyContent: 'center',
        alignItems: 'center',
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
    buttons: {
        margin: '6px 0 0',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center'
    },
    button2: {
        minWidth: '90px',
        fontSize: '18px',
        fontWeight: 600,
        margin: '0 5px',
        padding: '9px',
        lineHeight: '100%'
    },
    footerLink: {
        fontSize: '16px',
        fontWeight: 600,
        lineHeight: '14px',
    },
    footerClass: {
        height: '15px'
    },
})

/**
 * 
 * @param {{exitFunction: () => void; closeModals: boolean;}} props 
 * @returns 
 */

const downloadProjexModal = props => {
    const s = useStyles();
    const buttonStyles = useButtonStyles();
    const auth = AuthenticationStore.useContainer();
    const [isOpen, setOpen] = useState(true);

    const onClick = e => {
        e.preventDefault();
        setOpen(false);
        window.location.href = "https://github.com/shikataganaii/Pekora-Bootstrapper/releases";
    }

    useEffect(async () => {
        await new Promise(resolve => setTimeout(resolve, 3000));
        let tries = 0;
        let success;
        while ((success === null || success !== true) && tries < 10 && !props.closeModals) {
            tries += 1;
            multiGetPresence({userIds: [auth.userId]}).then(res => {
                if (res[0] && res[0]?.userPresenceType === "InGame") {
                    success = true;
                    props.exitFunction();
                }
            });
            await new Promise(resolve => setTimeout(resolve, 3000));
        }
    }, []);

    return <>
        {!props.closeModals && !isOpen && <InstructionsModal closeModals={props.closeModals} exitFunction={props.exitFunction} />}
        {!props.closeModals && isOpen && <NewModal footerClass={s.footerClass} exitFunction={props.exitFunction} title="" footerElements={<Link href="/help/install">
            <a className={`link2018 ${s.footerLink}`} href="/help/install">Click here for help</a>
        </Link>}>
            <div className={s.container}>
                <span className={s.iconLogo} />
                <p className={s.loadingText}>You're moments away from getting into the game!</p>
            </div>
            <div className={s.buttons}>
                <ActionButton onClick={onClick} buttonStyle={buttonStyles.newBuyButton} label="Download and Install Marine" className={s.button2} />
            </div>
        </NewModal>}
    </>
}

export default downloadProjexModal;