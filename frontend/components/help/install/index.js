import { createUseStyles } from "react-jss";
import FakeNavBar from '../fakenavbar'
import { useState } from "react";
import HelpSelector from '../helpselector'
import MarkdownContent from "../subpages/markdownContent";
import Footer from "../../footer";
import Link from "../../link";

const useStyles = createUseStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        marginTop: '40px',
        padding: '15px',
        flex: 1,
        textAlign: 'start',
    },
    content: {
        display: 'flex',
        flexDirection: 'row',
        textAlign: 'start',
    },
    selectorContainer: {
        flex: ' 0 0 17%',
        height: 'auto',
        overflow: 'hidden',
    },
    elementContainer: {
        flex: '1 0 66%',
        maxWidth: '66%',
        minWidth: '640px',
        padding: '0 30px',
        //marginTop: '35px'
    },

    subNav: {
        display: 'flex',
        flexDirection: 'row',
        gap: '15px 30px',
        justifyContent: 'space-between',
        marginBottom: '40px',
        flexWrap: 'wrap'
    },
    breadcrumbs: {
        padding: 0,
        margin: 0,
        '& li': {
            color: 'var(--text-color-primary)',
            fontSize: '13px',
            fontWeight: 300,
            display: 'inline',
            maxWidth: '450px',
            overflow: 'hidden',
            textOverflow: 'ellipsis',
        },
        '& li:not(:first-child)::before': {
            content: '">"',
            margin: '0 4px',
        }
    },
});

const InstallHelpPage = props => {
    const s = useStyles();

    const subpages = [
        {
            id: "play",
            name: "How to Install and Play Marine",
            el: () => <MarkdownContent mdUrl="installPlay.md" />,
        },
        {
            id: "trojan",
            name: "Marine detected as a trojan/virus",
            el: () => <MarkdownContent mdUrl="trojan.md" />,
        },
        {
            id: "msvcp140",
            name: "The code execution could not proceed because...",
            el: () => <MarkdownContent mdUrl="msvcp140.md" />,
        },
        {
            id: "error6",
            name: "An error occurred while starting Marine, Error Code: 6",
            el: () => <MarkdownContent mdUrl="errorCode6.md" />,
        },
        /*{
            id: "makeaccount",
            name: "How to make a Marine Account",
            el: () => <MarkdownContent mdUrl="createAccount.md" />,
        },
        {
            id: "creategame",
            name: "How to Create your own Game",
            el: () => <MarkdownContent mdUrl="createGame.md" />,
        }*/
    ]
    const [selected, setSelected] = useState(subpages[0]);
    /*subpages.filter(v => !!v).map(v => {
        v.onClick = (e) => {
            e.preventDefault();
            setSelected(v);
        };
        return v;
    });*/
    /*useEffect(() => {
        setSelected(subpages[0]);
    }, [props]);*/

    return <>
        <FakeNavBar />
        <div className={`container section-content ${s.container}`}>
            <div className={s.subNav}>
                <ol className={s.breadcrumbs}>
                    <li>
                        <Link href="/help">
                            <a className="link2018" style={{ fontWeight: 300, fontSize: '13px', lineHeight: 1.5 }} href="/help">Marine Support</a>
                        </Link>
                    </li>
                    <li>
                        <Link href="/help/install">
                            <a className="link2018" style={{ fontWeight: 300, fontSize: '13px', lineHeight: 1.5 }} href="/help/install">Playing Marine</a>
                        </Link>
                    </li>
                    <li>
                        <Link href="#">
                            <a className="link2018" style={{ fontWeight: 300, fontSize: '13px', lineHeight: 1.5 }} href="#">{selected?.name}</a>
                        </Link>
                    </li>
                </ol>
            </div>
            <div className={s.content}>
                <div className={s.selectorContainer}>
                    <span style={{ display: 'block', fontWeight: 'bold', fontSize: '15px' }}>Articles in this section</span>
                    <HelpSelector selected={selected} setSelected={(e) => {
                        setSelected(e);
                    }} options={subpages} />
                </div>
                <div className={s.elementContainer}>
                    {selected &&
                        <>
                            <h1 style={{ marginBottom: '10px', fontWeight: 700 }}>{selected.name}</h1>
                            {selected.el()}
                        </>
                    }
                </div>
            </div>
        </div>
        <Footer />
    </>
}

export default InstallHelpPage;