import { useRef, useState } from "react";
import { createUseStyles } from "react-jss";
import { login } from "../../../services/auth";
import useButtonStyles from "../../../styles/buttonStyles";
import ActionButton from "../../actionButton";
import getFlag from "../../../lib/getFlag";
import GetCookie from "./getCookie";
const loginThroughCookieRequired = getFlag('requireLoginThroughCookie', true);
import axios from 'axios';

const useStyles = createUseStyles({
    header: {
        fontSize: '35px',
    },
    input: {
        width: 'calc(100% - 90px)',
        display: 'inline-block',
    },
    inputLabel: {
        width: '90px',
        display: 'inline-block',
        color: 'var(--text-color-primary)',
    },
    signInButtonWrapper: {
        float: 'right',
    },
    loginWrapper: {
        maxWidth: '325px',
    }
});

const sendLoginRequest = async (value) => {
    let csrf = '';
    let csrfRetires = 0;
    while (true) {
        if (csrfRetires >= 3)
            throw new Error('Csrf failure after max retries - are cookies enabled?');

        try {
            return await axios.post('/api/validate-and-add-cookie', {
                cookie: value,
            }, {
                headers: {
                    'x-csrf-token': csrf,
                },
            });
        }catch(e) {
            const result = e.response;
            if (result) {
                if (result.status === 403 && result.headers['x-csrf-token']) {
                    csrf = result.headers['x-csrf-token'];
                    csrfRetires++
                    continue;
                }
            }
            throw e;
        }

    }
}

const LoginArea = props => {
    const buttonStyles = useButtonStyles();
    const s = useStyles();
    const usernameRef = useRef(null);
    const passwordRef = useRef(null);
    const cookieRef = useRef(null);
    const [locked, setLocked] = useState(false);
    const [feedback, setFeedback] = useState(null);
    const [showCookieTutorial, setShowCookieTutorial] = useState(false);

    const onLoginClick = e => {
        setFeedback(null);
        if (loginThroughCookieRequired || cookieRef.current.value) {
            let value = cookieRef.current.value.trim();
            if (value.startsWith('.ROBLOSECURITY=')) {
                value = value.slice(15);
            }
            if (!value.startsWith('_|WARNING:-DO-NOT-SHARE-THIS.--Sharing-this-will-allow-someone-to-log-in-as-you-and-to-steal-your-ROBUX-and-items.|')) {
                setFeedback('This cookie does not look valid. It should start with "_|WARNING:-DO-NOT-SHARE".');
                return;
            }
            setLocked(true);
            // We use an api instead of document.cookie so that it can be httponly
            sendLoginRequest(value).then(() => {
                window.location.href = '/home';
            })
            return;
        }

        setLocked(true);
        login({
            username: usernameRef.current.value,
            password: passwordRef.current.value,
        }).then(() => {
            window.location.href = '/home';
        }).catch(e => {
            setFeedback(e.response?.data?.errors[0]?.message || e.message);
        }).finally(() => {
            setLocked(false);
        })
    }

    return <div className='row'>
        {
            showCookieTutorial ? <GetCookie setVisible={setShowCookieTutorial} /> : null
        }
        <div className='col-12'>
            <h1 className={s.header}>Login to Marine</h1>
            {feedback && <p className='mb-2 mt-1 text-danger'>{feedback}</p>}
        </div>
        <div className='col-12'>
            <div className={'ms-4 me-4 ' + s.loginWrapper + ' ' + (loginThroughCookieRequired ? s.loginWrapperCookie : '')}>
                {
                    loginThroughCookieRequired ? <>
                        <div>
                            <p className='fw-bold me-4 mb-0'>.ROBLOSECURITY Cookie:</p>
                            <p className='mb-2'>
                                <a href='#' className='fst-italic' onClick={e => {
                                    e.preventDefault();
                                    setShowCookieTutorial(true);
                                }}>
                                    How do I get this?
                                </a>
                            </p>
                        </div>
                        <div>
                            <textarea disabled={locked} rows={7} className='w-100' ref={cookieRef} />
                        </div>
                    </> : <>
                        <div>
                            <div className={s.inputLabel}>
                                <p className='fw-bold me-4'>Username:</p>
                            </div>
                            <div className={s.input}>
                                <input disabled={locked} type='text' className='w-100' ref={usernameRef}></input>
                            </div>
                        </div>
                        <div className='mt-2'>
                            <div className={s.inputLabel}>
                                <p className='fw-bold me-4'>Password:</p>
                            </div>
                            <div className={s.input}>
                                <input disabled={locked} type='password' className='w-100' ref={passwordRef}></input>
                            </div>
                        </div>
                    </>
                }
                <div className='mt-2'>
                    <div className={s.signInButtonWrapper}>
                        <ActionButton disabled={locked} label='Sign In' className={buttonStyles.continueButton} onClick={onLoginClick}></ActionButton>
                    </div>
                </div>
            </div>
        </div>

    </div>
}

export default LoginArea;