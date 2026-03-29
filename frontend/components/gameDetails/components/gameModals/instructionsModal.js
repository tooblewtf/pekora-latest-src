import React, { useState } from "react";
import { createUseStyles } from "react-jss";
import ActionButton from "../../../actionButton";
import useButtonStyles from "../../../../styles/buttonStyles";
import NewModal from "../../../newModal";
import Link from "next/link";
import { launchGame } from "../../../../services/games";
import GameDetailsStore from "../../stores/gameDetailsStore";
import { joinGame } from "../newPlayButton";
import AuthenticationStore from "../../../../stores/authentication";
import { forEach } from "lodash";

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
        backgroundSize: '95px 95px',
        backgroundPosition: '0 0'
    },
    buttons: {
        margin: '6px 0 0',
    },
    button2: {
        minWidth: '90px',
        fontSize: '18px',
        fontWeight: 600,
        margin: '0 5px',
    },

    joinButtonWrapper: {
        marginTop: '30px',
        width: 'auto',
    },
    joinButton: {
        fontSize: '21px',
        margin: '12px auto 0',
        borderRadius: '5px',
        width: '100%',
        padding: '6px 12px',
        fontWeight: 600,
        lineHeight: '1.428571429'
    },
    instructionList: {
        '& li:first-child': {
            paddingLeft: 0
        },
        '& li:last-child': {
            paddingRight: 0,
            borderRight: 0
        },
    },
    instruction: {
        float: 'left',
        padding: '0 12px',
        width: '25%',
        textAlign: 'left',
        '& h2': {
            fontSize: '20px',
            fontWeight: 700,
            lineHeight: '1em',
            padding: '5px 0',
            margin: 0,
        },
        '& p': {
            whiteSpace: 'pre-line',
            lineHeight: '1.5em',
            margin: '0 0 15px',
            padding: 0,
            height: '4.5em',
            wordWrap: 'break-word',
            hyphens: 'none',
        }
    },
    footer: {
        display: 'flex',
        justifyContent: 'space-between'
    },
})

/**
 * 
 * @param {{exitFunction: () => void; closeModals: boolean;}} props 
 * @returns 
 */

const instructionsModal = props => {
    const s = useStyles();
    const buttonStyles = useButtonStyles();
    const store = GameDetailsStore.useContainer();
    const auth = AuthenticationStore.useContainer();
    const [error, setError] = useState(null);
    // TODO: implement error here

    const instructions = [
        {
            textElement: () => {
                return <p>Click the <b>MarinePlayerLauncher.exe</b> to run the Marine installer, which just downloaded via your web browser.</p>
            },
            element: () => {
                return <img src='/img/instructions/one.png' style={{ verticalAlign: 'middle', border: 0, marginTop: '60px' }}></img>
            }
        },
        {
            textElement: () => {
                return <p>Click <b>run</b> when prompted to begin the installation process.</p>
            },
            element: () => {
                return <img src='/img/instructions/two.png' style={{ verticalAlign: 'middle', border: 0 }}></img>
            }
        },
        {
            textElement: () => {
                return <p>Click <b>Ok</b> once you've successfully installed Marine.</p>
            },
            element: () => {
                return <img src='/img/instructions/three.png' style={{ verticalAlign: 'middle', border: 0 }}></img>
            }
        },
        /*{
            textElement: () => {
                return <p>Your antivirus may detect Marine as a false-positive virus, because it is unsigned. To fix this, you must <b>add the Marine installer and "%localappdata%/ProjectX"</b> to your <b>antivirus' exclusion list</b> and relaunch the installer.</p>
            },
            element: () => {
                return <img src='/img/instructions/three.png' style={{ verticalAlign: 'middle', border: 0 }}></img>
            }
        },*/
        {
            textElement: () => {
                return <p>After installation, click <b>Join</b> below to join the action!</p>
            },
            element: () => {
                return <div className={s.joinButtonWrapper}>
                    <ActionButton className={s.joinButton} label="Join" buttonStyle={buttonStyles.newBuyButton} onClick={e => joinGame(e, store.details.id, auth, setError)} />
                </div>
            }
        }
    ]

    return <>
        {!props.closeModals && <NewModal containerWidth={1000} footerClass={s.footer} exitFunction={props.exitFunction} title="Thanks for playing Marine" footerElements={<>
            <span style={{ color: 'var(--text-color-secondary)', fontSize: '10px', fontWeight: 600 }}>The Marine installer should download shortly. If it doesn't, start the <Link href="https://github.com/shikataganaii/Pekora-Bootstrapper/releases">
                <a className="link2018" href="https://github.com/shikataganaii/Pekora-Bootstrapper/releases">download now.</a>
            </Link></span>
            <span style={{ float: "right", color: 'var(--text-color-secondary)', fontSize: '10px', fontWeight: 600 }}>Having trouble installing? Click <Link href="https://github.com/shikataganaii/Pekora-Bootstrapper/releases">
                <a className="link2018" href="/help/install">here for help.</a>
            </Link></span>
        </>}>
            <div className={s.container}>
                <ul className={s.instructionList} style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                    {instructions.map((instruction, index) => {
                        const currentInstruct = index + 1
                        return <li className={s.instruction}>
                            <h2>{currentInstruct}</h2>
                            <instruction.textElement />
                            <instruction.element />
                        </li>
                    })}
                </ul>
            </div>
        </NewModal >}
    </>
}

export default instructionsModal;