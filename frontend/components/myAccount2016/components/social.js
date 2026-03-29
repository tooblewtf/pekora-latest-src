import React, { useEffect, useState } from "react";
import { createUseStyles } from "react-jss";
import AuthenticationStore from "../../../stores/authentication";
import useCardStyles from "../../userProfile/styles/card";
import Subtitle from "./subtitle";
import { getUserConnections, setUserConnections } from "../../../services/accountInformation";
import { ConvertSocialToHuman } from "../../userProfile/components/description";
import { IsNullOrEmpty, wait } from "../../../lib/utils";
import ActionButton from "../../actionButton";
import buttonStyles from "../../../styles/buttonStyles";
import Feedback from "../../../stores/feedback";
import { FeedbackType } from "../../../models/feedback";

const useStyles = createUseStyles({
    label: {
        color: 'var(--text-color-quinary)',
        marginBottom: 0,
        fontSize: '15px',
        "&:not(:first-child)": {
            marginTop: 6,
        },
    },
    saveBtnContainer: {
        display: "flex",
        justifyContent: "end",
        marginTop: 6,
    },
    saveBtn: {
        padding: 9,
        margin: "0 5px",
        width: 90,
        fontSize: "18px!important",
        fontWeight: "400!important",
        lineHeight: "100%!important",
    },
    loadingModal: {
        position: 'absolute',
        top: 40,
        left: 0,
        width: '100vw',
        height: '100vh',
        backgroundColor: 'rgba(0,0,0,0.7)',
        zIndex: 1000
    },
    loadingContainer: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        flexDirection: 'column',
        gap: 12,
        margin: "auto",
        width: "100%",
        height: "100%",
    },
    loadingIcon: {
        background: 'url(/img/loading_old.gif)',
        backgroundSize: '48px 17px',
        width: '48px',
        height: '17px',
    },
})

const Social = () => {
    const auth = AuthenticationStore.useContainer();
    const cardStyles = useCardStyles();
    const s = useStyles();
    const btnStyles = buttonStyles();
    const [conns, setConns] = useState(null);
    const [isSaving, setSaving] = useState(false);
    const feedback = Feedback.useContainer();
    
    useEffect(async () => {
        const userCons = await getUserConnections({ userId: auth.userId });
        await setConns(userCons);
    }, []);
    
    // return <a title={ConvertSocialToHuman(platform)} className={s2.connectionLink} href={url} target="_blank">
    //     <span className={`social-link-icon ${platform}`} />
    // </a>
    
    if (!conns) return <div className='row'>
        <div className='col-12'>
            <span className="spinner w-100" style={{ height: "36px", backgroundSize: "auto 36px" }}/>
        </div>
    </div>
    
    return <div className='row'>
        {
            isSaving ? <div className={s.loadingModal}>
                <div className={s.loadingContainer}>
                    <span className={s.loadingIcon}></span>
                    <span style={{ color: "#fff" }}>Saving social network...</span>
                </div>
            </div> : null
        }
        <div className='col-12'>
            <Subtitle>Social Networks</Subtitle>
            <div className={cardStyles.card + ' p-3'}>
                {
                    Object.entries(conns).map(([platform, value]) => {
                        if (platform.toLowerCase() === "promotionchannelsvisibilityprivacy" ||
                            platform.toLowerCase() === "facebook" ||
                            platform.toLowerCase() === "guilded"
                        ) return null;
                        let isDefault = true;
                        if (value && value.startsWith("@")) {
                            setConns(prev => ({
                                ...prev,
                                [platform]: value.slice(1),
                            }));
                        }
                        return <>
                            <p className={s.label}>{platform.toLowerCase() !== "discord" ? ConvertSocialToHuman(platform) : "Discord (can be user id)"}</p>
                            <input
                                type="text"
                                placeholder="e.g. @MARINE"
                                onChange={e => {
                                    if (isSaving) return;
                                    isDefault = false;
                                    setConns(prev => ({
                                        ...prev,
                                        [platform]: e.target.value.charAt(0) == "@" ? e.target.value.slice(1) : IsNullOrEmpty(e.target.value) ? null : e.target.value,
                                    }));
                                }}
                                disabled={isSaving}
                                className={`inputTextStyle ${IsSocialHandleInvalid(platform, value) && !value.startsWith("@") && isDefault ? "hasError" : ""} ${s.inputStyle}`}
                                defaultValue={value ? "@" + value : null}
                                maxLength={39}
                            />
                        </>
                    })
                }
                <ActionButton
                    label="Save"
                    buttonStyle={btnStyles.newCancelButton}
                    onClick={async () => {
                        await setSaving(true);
                        try {
                            await setUserConnections({ connections: conns });
                            feedback.addFeedback("Social network saved", FeedbackType.SUCCESS);
                        } catch (e) {
                            feedback.addFeedback(e.message, FeedbackType.ERROR);
                        }
                        await wait(0.5);
                        await setSaving(false);
                    }}
                    className={s.saveBtn}
                    divClassName={s.saveBtnContainer}
                />
            </div>
        </div>
    </div>
}

export default Social;

function IsSocialHandleInvalid(social, value) {
    if (IsNullOrEmpty(value)) return false;
    const regexMap = {
        twitter: /^[_A-Za-z][A-Za-z0-9_]{3,14}$/,
        youtube: /^(?![.-])(?!.*[.]{2})[A-Za-z0-9._-]{3,30}$/,
        tiktok: /^(?!.*\.\.)(?!.*\.$)[A-Za-z0-9._]{2,24}$/,
        discord: /^[a-z0-9._]{2,32}$/,
        telegram: /^[A-Za-z][A-Za-z0-9_]{4,31}$/,
        twitch: /^[a-z][a-z0-9_]{3,24}$/,
        github: /^(?!-)(?!.*--)[A-Za-z0-9-]{1,39}(?<!-)$/,
        roblox: /^(?=.{3,20}$)(?!.*__)(?!.*_$)(?!^_)[A-Za-z0-9_]+$/,
    };
    const regex = regexMap[social];
    if (!regex) return true;
    return !regex.test(value);
}
