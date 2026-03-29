'use client'

import HelpPage from "../../components/help";
import Theme2016 from "../../components/theme2016";
const GodHelpMe = props => {
    return (
        <Theme2016>
            <HelpPage />
        </Theme2016>
    );
}

export const getStaticProps = () => {
    return {
        props: {
            title: 'Help - Marine',
            disableWebsiteTheming: true,
        },
    };
};

export default GodHelpMe;