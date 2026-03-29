import {createUseStyles} from "react-jss";
import ActionButton from "../../components/actionButton";
import Link from "../../components/link";
import useButtonStyles from "../../styles/buttonStyles";
import Theme2016 from "../../components/theme2016";

const useStyles = createUseStyles({
    wrapper: {
        display: "flex",
        marginLeft: "auto",
    },
    container: {
        minHeight: 400,
        margin: "30px auto",
        backgroundColor: "#fff",
        marginLeft: 175,
        "@media(max-width: 970px)": {
            marginLeft: 0,
        },
    },
    gamesBtn: {
        padding: "7px 16px",
        lineHeight: "100%",
        borderRadius: 3,
        transition: "all 0.2s ease-in-out",
        fontSize: 16,
        fontWeight: 300,
    },
});

function ThankYouPage() {
    const s = useStyles();
    const buttonStyles = useButtonStyles();
    
    return <Theme2016>
        <div className={`container ssp ${s.wrapper}`}>
            <div className={`w-100 flex justify-content-center align-items-center ${s.container}`}>
                <div className={`flex flex-column justify-content-center align-items-center w-100`}
                     style={{ padding: "15px 0" }}>
            <span className="icon-games" style={{
                backgroundPosition: "0 -150px",
                height: 150,
                width: 150,
                backgroundSize: "200% auto",
            }}/>
                    <h1 style={{
                        textAlign: "center",
                        width: "100%",
                        color: "var(--success-color)",
                        margin: "10px 0",
                        lineHeight: "1.3em",
                        fontSize: 48,
                        fontWeight: 300,
                    }}>Thank you for installing Marine.</h1>
                    <span style={{
                        width: "500px",
                        margin: "50px auto",
                        fontSize: 16,
                        fontWeight: 300,
                        textAlign: "center",
                        lineHeight: "1.5em",
                    }}>It"s time to play! You can now browse and play the most popular games on Marine.</span>
                    <Link href="/games">
                        <a href="/games">
                            <ActionButton label="Browse Marine Games" buttonStyle={buttonStyles.newBuyButton}
                                          className={s.gamesBtn}/>
                        </a>
                    </Link>
                </div>
            </div>
        </div>
    </Theme2016>
}

export default ThankYouPage;
