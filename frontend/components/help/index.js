import { createUseStyles } from "react-jss";
import FakeNavBar from './fakenavbar'
import { useState } from "react";
import HelpSelector from './helpselector'
import Footer from ".././footer";
import Link from "../link";

const useSelectorStyles = createUseStyles({
    list: {
        display: 'flex',
        borderRadius: '3px',
        flexDirection: 'column',
        justifyContent: 'center',
        margin: '0 15px 30px',
        flex: '1 0 340px',
        maxWidth: '100%',
        border: '1px solid transparent',
        color: 'var(--primary-color)',
        backgroundColor: 'var(--white-color)',
        cursor: 'pointer',
        '&:hover': {
            color: 'var(--text-color-primary)',
            backgroundColor: 'var(--primary-color)',
            boxShadow: 'none',
            '& span': {
                color: 'var(--text-color-primary)',
            }
        },
    },
    wrapper: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        justifyContent: 'center',
        borderRadius: 'inherit',
        color: 'var(--primary-color)',
        padding: '20px 30px',
        background: 'none',
        userSelect: 'none',
        '&:hover': {
            color: 'inherit',
            boxShadow: 'none'
        }
    },
    title: {
        fontSize: '18px',
        fontWeight: 400,
        color: 'inherit',
        lineHeight: '1.5'
    },
    description: {
        marginTop: '10px',
        fontWeight: 400,
        margin: 0,
        color: 'var(--text-color-tertiary)',
        fontSize: '14px',
    },
});

/**
 * 
 * @param {{name: string; description: string; url: string;}} props 
 * @returns 
 */
const ArticleOption = props => {
    const s = useSelectorStyles();
    return <Link href={props.url}>
        <li className={`${s.list} section-content`}>
            <a className={`${s.wrapper}`}>
                <span className={`${s.title}`}>{props.name}</span>
                <span className={`${s.description}`}>{props.description}</span>
            </a>
        </li>
    </Link>
}

const useStyles = createUseStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        margin: '0 auto',
        padding: '12px',
        width: '90%',
        backgroundColor: 'transparent!important',
        boxShadow: 'none!important',
        flex: 1,
    },
    articleContainer: {
        margin: '0 -15px',
        display: 'flex',
        flexDirection: 'row',
        flexWrap: 'wrap',
        justifyContent: 'flex-start',
        listStyle: 'none',
        padding: 0,
        marginBottom: '30px',
    },
    banner: {
        backgroundImage: 'url(/img/0ec15ee53d17dd2c567ecd6c4196ef6a529ea694.jpg)',
        //marginBottom: '30px',
        backgroundPosition: 'center',
        backgroundSize: 'cover',
        height: '300px',
        padding: '0 20px',
        width: '100%',
        zIndex: 9,
        position: 'relative',
    },
});

const HelpPage = props => {
    const s = useStyles();

    const articles = [
        {
            name: "Playing Marine",
            description: "I'm having trouble playing Marine",
            url: "/help/install",
        },
        {
            name: "Creating on Marine",
            description: "I'm having trouble creating on Marine", // include guides on how to make games, game limits, studios, etc
            url: "/help/create",
        },
        {
            name: "Marine Account",
            description: "I need help with my Marine account",
            url: "/help/account",
        },
        {
            name: "Earning Robux or Tickets",
            description: "I need help figuring out how to make Robux and Tickets",
            url: "/help/earn",
        },
    ]

    return <>
        <FakeNavBar />
        <div className={s.banner} />
        <div className={`container ${s.container}`}>
            <span style={{ textAlign: 'center', fontSize: '48px', fontWeight: 800, marginBottom: '30px' }}>Marine Help Articles</span>
            <div className={s.articleContainer}>
                {// eventually put search here? for now, span
                }
                {articles && articles.map(v => {
                    return <ArticleOption name={v.name} description={v.description} url={v.url} />
                })}
            </div>
        </div>
        <Footer />
    </>
}

export default HelpPage;