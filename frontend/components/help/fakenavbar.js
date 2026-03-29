import { createUseStyles } from "react-jss";
import Link from ".././link";

const useStyles = createUseStyles({
    fakeNav: {
        position: 'fixed',
        height: '40px',
        backgroundColor: 'var(--secondary-color)',
        boxShadow: 'rgba(25, 25, 25, 0.3) 0px 3px 3px -3px',
        backgroundRepeat: 'repeat-x',
        verticalAlign: 'middle',
        padding: 0,
        zIndex: 1030,
        top: 0,
        left: 0,
        right: 0
    },
    navContainer: {
        maxWidth: '100%!important',
        width: '100%!important',
        height: '100%!important',
        padding: 0,
    },
    logoContainer: {
        margin: '0 12px',
        height: '100%',
        maxWidth: '118px',
        alignItems: 'center',
    },
    logoNav: {
        backgroundImage: 'url(/img/logo.png)',
        backgroundRepeat: 'no-repeat',
        backgroundPosition: 'center',
        backgroundSize: '118px 30px',
        width: '118px',
        height: '40px',
        display: 'inline-block'
    },
});

const InstallHelpPage = (e) => {
    const s = useStyles();

    return <nav className={`navbar fixed-top ${s.fakeNav}`}>
        <div className={s.navContainer}>
            <div className={s.logoContainer}>
                <Link href="/home">
                    <a className={`${s.logoNav} col-lg-3`} href="/home"></a>
                </Link>
            </div>
        </div>
    </nav>
}

export default InstallHelpPage;