import {createUseStyles} from "react-jss";
import {useRouter} from "next/router";
import useButtonStyles from "../../styles/buttonStyles";
import Theme2016 from "../theme2016";
import ActionButton from "../actionButton";
import Head from "next/head";

const useStyles = createUseStyles({
    container: {
        '& img': {
            height: '300px',
            width: '300px',
            margin: '20px 0',
            display: 'block',
        }
    },
    buttonContainer: {
        padding: '3px 0',
        gap: '12px',
        display: 'flex',
        '& button': {
            padding: 9,
            width: '90px',
            lineHeight: '18px',
            fontSize: 18
        }
    },
    textContainer: {
        '& *': {
            marginBottom: 12,
            padding: '5px 0',
            lineHeight: '1em',
            textAlign: 'start',
        }
    },
});

const getCodeImage = (code) => {
    switch(parseInt(code)) {
        case 404:
        case 504:
            return "404.png";
        case 403:
        case 401:
            return "403.png";
        default:
            return "500.png";
    }
}

/**
 *
 * @param code {number?}
 * @param title {string?}
 * @param desc {string?}
 * @returns {JSX.Element}
 * @constructor
 */
const ErrorPage = ({ code, title, desc }) => {
    const s = useStyles();
    const router = useRouter();
    const buttonStyles = useButtonStyles();
    
    if (!code) code = 500;
    if (!title) title = "Internal Server Error";
    if (!desc) desc = "An unexpected error occurred";
    
    return <>
        <Head>
            <title>{code} - Marine</title>
        </Head>
        <Theme2016>
            <div className='col-12 h-100 flex justify-content-center align-items-center' style={{
                maxWidth: 970,
                width: 800,
                margin: '0 auto',
                marginTop: '10%',
            }}>
                <div className={`${s.container} section-content flex justify-content-between`}>
                    <div className='flex justify-content-between flex-column w-50'>
                        <div className={s.textContainer}>
                            <h3 style={{fontSize: '32px', fontWeight: 500}}>{title}</h3>
                            <h4 style={{fontSize: '16px', fontWeight: 400}}>{code}<span style={{padding: '0 5px'}}>|</span>{desc}</h4>
                        </div>
                        <div className={s.buttonContainer}>
                            <ActionButton buttonStyle={buttonStyles.newBuyButton} label='Back' onClick={e => {
                                e.preventDefault();
                                router.back();
                            }}/>
                            <ActionButton buttonStyle={buttonStyles.newCancelButton} label='Home' onClick={e => {
                                e.preventDefault();
                                router.push('/home');
                            }}/>
                        </div>
                    </div>
                    <img src={`/img/${getCodeImage(code)}`} alt={code.toString()} />
                </div>
            </div>
        </Theme2016>
    </>
}

export default ErrorPage;