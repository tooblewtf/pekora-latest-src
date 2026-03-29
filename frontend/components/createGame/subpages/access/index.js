import {createUseStyles} from "react-jss";

const useStyles = createUseStyles({
    contain: {
        margin: 0,
        maxWidth: '45%!important',
    },
    settingsContainer: {
        display: "flex",
        flexDirection: "column",
        '& span': {
            marginBottom: 3
        },
        '& textarea': {
            resize: "none",
            height: 125
        },
        '& select': {
        
        }
    },
    input: {
        marginBottom: 10
    },
    deviceLabel: {
        display: "flex",
        gap: '8px',
        marginBottom: '5px',
        '& span': {
            margin: 0
        }
    },
    section: {
        marginBottom: 20,
        display: 'flex',
        flexDirection: 'column',
    },
})

const Access = props => {
    const s = useStyles();
    
    const handleChange = (event) => {
        const {name, checked} = event.target;
        props.setPlayableDevices((prevDevices) => ({
            ...prevDevices,
            [name]: checked,
        }));
    };
    
    return <div className={`container noPadding ${s.contain}`}>
        <h2 style={{marginBottom: 15}}>Access</h2>
        <div className={s.settingsContainer}>
            <div className={s.section}>
                <span>Playable devices:</span>
                <label className={s.deviceLabel}>
                    <input type="checkbox" disabled={true} name="computer" checked={props.playableDevices.computer}
                           onChange={handleChange}/>
                    <span>Computer</span>
                </label>
                <label className={s.deviceLabel}>
                    <input type="checkbox" disabled={true} name="phone" checked={props.playableDevices.phone}
                           onChange={handleChange}/>
                    <span>Phone</span>
                </label>
                <label className={s.deviceLabel}>
                    <input type="checkbox" disabled={true} name="tablet" checked={props.playableDevices.tablet}
                           onChange={handleChange}/>
                    <span>Tablet</span>
                </label>
                <label className={s.deviceLabel}>
                    <input type="checkbox" disabled={true} name="console" checked={props.playableDevices.console}
                           onChange={handleChange}/>
                    <span>Console</span>
                </label>
            </div>
            
            <div className={s.section}>
                <span>Maximum Player Count:</span>
                <div>
                    <select
                        value={props.playerCount}
                        className="br-none border-1 border-secondary pe-2"
                        onChange={(v) => {
                            props.setPlayerCount(parseInt(v.currentTarget.value, 10));
                        }}
                    >
                        {[...new Array(99)].map((_, i) => {
                            return (
                                <option value={i + 1} key={i}>
                                    {i + 1}
                                </option>
                            );
                        })}
                    </select>
                </div>
            </div>
            
            <div className={s.section}>
                <span>Game Year:</span>
                <div>
                    <select
                        value={props.gameYear}
                        className="br-none border-1 border-secondary pe-2"
                        onChange={(v) => {
                            props.setGameYear(parseInt(v.currentTarget.value, 10));
                        }}
                    >
                        <option value={2017}>2017</option>
                        <option value={2018}>2018</option>
                        <option value={2020}>2020</option>
                        <option value={2021}>2021</option>
                    </select>
                </div>
            </div>
            
            <div className={s.section}>
                <span>Server Fill:</span>
                <label className={s.deviceLabel}>
                    <input type="radio" name="optimize" checked={false} disabled={true}/>
                    <span>Marine optimizes server fill for me</span>
                </label>
                <label className={s.deviceLabel}>
                    <input type="radio" name="fill" checked={true} disabled={true}/>
                    <span>Fill each server as full as possible</span>
                </label>
                <label className={s.deviceLabel}>
                    <input type="radio" name="custom" checked={false} disabled={true}/>
                    <span>Customize how many server slots to reserve</span>
                </label>
            </div>
            
            <div className={s.section}>
                <span>Access:</span>
                <div>
                    <select
                        value={props.access}
                        className="br-none border-1 border-secondary pe-2"
                        onChange={(v) => {
                            props.setAccess(v.currentTarget.value);
                        }}
                        disabled={true}
                    >
                        <option value='Everyone' key='Everyone'>Everyone</option>
                    </select>
                </div>
            </div>
            
            
            <div className={s.section}>
                <span>VIP Servers:</span>
                <label className={s.deviceLabel}>
                    <input type="checkbox" checked={false} disabled={true}
                        //onChange={handleChange}
                    />
                    <span>Allow VIP Servers</span>
                </label>
            </div>
        </div>
    </div>
}

export default Access;